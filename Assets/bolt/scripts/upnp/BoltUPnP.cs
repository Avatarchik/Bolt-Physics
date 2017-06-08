#if BOLT_UPNP_SUPPORT && UNITY_STANDALONE
using Mono.Nat;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using UdpKit;
using UnityEngine;

namespace BoltInternal {
  public class StandaloneNatCommunicator : BoltInternal.NatCommunicator {

    class NatPortMappingChanged {
      public NatDeviceState Device;
      public NatPortMapping Mapping;
    }

    class NatPortMapping : Bolt.IPortMapping {
      public volatile int External;
      public volatile int Internal;
      public volatile Bolt.NatPortMappingStatus Status;

      ushort Bolt.IPortMapping.External {
        get { return (ushort)External; }
      }

      ushort Bolt.IPortMapping.Internal {
        get { return (ushort)Internal; }
      }

      Bolt.NatPortMappingStatus Bolt.IPortMapping.Status {
        get { return Status; }
      }

      public NatPortMapping Clone() {
        return (NatPortMapping)MemberwiseClone();
      }

      public override int GetHashCode() {
        return External ^ Internal;
      }

      public override bool Equals(object obj) {
        var that = obj as NatPortMapping;
        if (that != null) {
          return this.Internal == that.Internal && this.External == that.External;
        }
        else {
          return false;
        }
      }

      public override string ToString() {
        return string.Format("[PortMapping External={0} Internal={1} Status={2}]", External, Internal, Status);
      }


    }

    class NatDeviceState : Bolt.INatDevice {
      public volatile INatDevice Nat;
      public volatile IPAddress ExternalAddress;
      public volatile Dictionary<int, NatPortMapping> PortMappings = new Dictionary<int, NatPortMapping>();

      string Bolt.INatDevice.DeviceType {
        get { lock (syncLock) { return Nat.DeviceType; } }
      }

      UdpIPv4Address Bolt.INatDevice.PublicAddress {
        get { lock (syncLock) { return ExternalAddress == null ? UdpIPv4Address.Any : UdpIPv4Address.Parse(ExternalAddress.ToString()); } }
      }

      UdpIPv4Address Bolt.INatDevice.LocalAddress {
        get { lock (syncLock) { return UdpIPv4Address.Parse(Nat.LocalAddress.ToString()); } }
      }

      IEnumerable<Bolt.IPortMapping> Bolt.INatDevice.Ports {
        get { lock (syncLock) { return PortMappings.Values.Cast<Bolt.IPortMapping>().ToArray(); } }
      }

      public override string ToString() {
        return string.Format("[NatDevice Local={0} Public={1}]", ((Bolt.INatDevice)this).LocalAddress, ((Bolt.INatDevice)this).PublicAddress);
      }
    }

    class BoltLogTextWriter : TextWriter {
      public override System.Text.Encoding Encoding {
        get { return System.Text.Encoding.UTF8; }
      }

      public override void WriteLine(string value) {
        BoltLog.Info("UPnP: " + value);
      }

      public override void WriteLine(string format, params object[] args) {
        BoltLog.Info("UPnP: " + format, args);
      }
    }

    static readonly object syncLock = new object();
    static List<NatDeviceState> deviceList = new List<NatDeviceState>();
    static Queue<NatPortMappingChanged> portChanges = new Queue<NatPortMappingChanged>();
    static Dictionary<int, NatPortMapping> portList = new Dictionary<int, NatPortMapping>();

    static StandaloneNatCommunicator() {
      NatUtility.DeviceLost += NatUtility_DeviceLost;
      NatUtility.DeviceFound += NatUtility_DeviceFound;
    }

    public override bool IsEnabled {
      get { return NatUtility.IsEnabled; }
    }

    public override bool NextPortStatusChange(out Bolt.INatDevice device, out Bolt.IPortMapping mapping) {
      lock (syncLock) {
        if (portChanges.Count > 0) {
          var change = portChanges.Dequeue();

          device = change.Device;
          mapping = change.Mapping;
          return true;
        }
        else {
          device = null;
          mapping = null;
          return false;
        }
      }
    }

    public override void OpenPort(int port) {
      UpdatePort(Bolt.NatPortMappingStatus.Open, port);
    }

    public override void ClosePort(int port) {
      UpdatePort(Bolt.NatPortMappingStatus.Closed, port);
    }

    void UpdatePort(Bolt.NatPortMappingStatus status, int port) {
      if (port < 1 || port > ushort.MaxValue) {
        throw new System.ArgumentOutOfRangeException("port");
      }

      lock (syncLock) {
        NatPortMapping mapping = new NatPortMapping { Status = status, Internal = port, External = port };

        if (portList.ContainsKey(port)) {
          portList.Remove(port);
        }

        portList.Add(port, mapping);
      }
    }

    public override IEnumerable<Bolt.INatDevice> NatDevices {
      get { lock (syncLock) { return deviceList.Cast<Bolt.INatDevice>().ToArray(); } }
    }

    public override void Update() {
      lock (syncLock) {
        foreach (var mp in portList.Values) {
          foreach (var dev in deviceList) {
            NatPortMapping devMp;

            if (dev.PortMappings.TryGetValue(mp.External, out devMp)) {
              if (devMp.Status != mp.Status) {
                switch (mp.Status) {
                  case Bolt.NatPortMappingStatus.Open: NatUtility_OpenPort(dev, devMp.External); break;
                  case Bolt.NatPortMappingStatus.Closed: NatUtility_ClosePort(dev, devMp.External); break;
                }
              }
            }
            else {
              devMp = mp.Clone();
              devMp.Status = Bolt.NatPortMappingStatus.Unknown;
              dev.PortMappings.Add(devMp.External, devMp);
            }
          }
        }
      }
    }

    public override void Disable(bool async) {
      lock (syncLock) {
        portList = new Dictionary<int, NatPortMapping>();
        deviceList = new List<NatDeviceState>();

        NatUtility.ShutdownThread(async);

        BoltLog.Info("UPnP Disabled");
      }
    }

    public override void Enable() {
      lock (syncLock) {
        //NatUtility.Logger = new BoltLogTextWriter();
        NatUtility.StartDiscovery();
        BoltLog.Info("UPnP Enabled");
      }
    }

    static void NatUtility_DeviceFound(object sender, DeviceEventArgs e) {
      lock (syncLock) {
        foreach (var device in deviceList) {
          if (device.Equals(e.Device)) {
            return;
          }
        }

        NatDeviceState deviceState;
        deviceState = new NatDeviceState { Nat = e.Device };
        deviceState.PortMappings = new Dictionary<int, NatPortMapping>();
        deviceList.Add(deviceState);

        BoltLog.Info("Found {0}", deviceState);

        NatUtility_FindPublicAddress(deviceState);
      }
    }

    static void NatUtility_DeviceLost(object sender, DeviceEventArgs e) {
      lock (syncLock) {
        for (int i = 0; i < deviceList.Count; ++i) {
          if (deviceList[i].Equals(e.Device)) {
            deviceList.RemoveAt(i);
            i -= 1;
          }
        }

        BoltLog.Info("Lost NAT device at {0}", e.Device.LocalAddress);
      }
    }

    static void NatUtility_FindPublicAddress(NatDeviceState device) {
      lock (syncLock) {
        device.Nat.BeginGetExternalIP(ar => {
          lock (syncLock) {
            try {
              device.ExternalAddress = device.Nat.EndGetExternalIP(ar);
              BoltLog.Info("Found external address of {0}", device);
            }
            catch (Exception exn) {
              BoltLog.Exception(exn);
            }
          }
        }, null);
      }
    }

    static void NatUtility_OpenPort_Finish(NatDeviceState device, int port) {
      try {
        var natMapping = device.PortMappings.Values.FirstOrDefault(p => p.Internal == port && p.External == port);
        if (natMapping != null) {
          // set this port as open
          natMapping.Status = Bolt.NatPortMappingStatus.Open;

          // tell user about this
          portChanges.Enqueue(new NatPortMappingChanged { Device = device, Mapping = natMapping.Clone() });

          // meep
          BoltLog.Info("Changed {0} on {1}", natMapping, device);
        }
        else {
          BoltLog.Warn("Received incorrect port mapping result from {0}", device);
        }
      }
      catch (Exception exn) {
        BoltLog.Exception(exn);
      }
    }

    static void NatUtility_OpenPort(NatDeviceState device, int port) {
      lock (syncLock) {
        Mapping mapping = new Mapping(Protocol.Udp, port, port);
        device.Nat.BeginCreatePortMap(mapping, ar => {
          lock (syncLock) {
            try {
              device.Nat.EndCreatePortMap(ar);

              // finish this
              NatUtility_OpenPort_Finish(device, port);
            }
            catch (MappingException exn) {
              if (exn.ErrorCode == 718) {
                NatUtility_OpenPort_Finish(device, port);
              }
              else {
                BoltLog.Exception(exn);
              }
            }
            catch (Exception exn) {
              BoltLog.Exception(exn);
            }
          }
        }, null);
      }
    }

    static void ClosePortMapping(NatDeviceState device, int port) {
      var natMapping = device.PortMappings.Values.FirstOrDefault(p => p.Internal == port && p.External == port);
      if (natMapping != null) {
        // set this port as open
        natMapping.Status = Bolt.NatPortMappingStatus.Closed;

        // tell user about this
        portChanges.Enqueue(new NatPortMappingChanged { Device = device, Mapping = natMapping.Clone() });

        // meep
        BoltLog.Info("Changed {0} on {1}", natMapping, device);
      }
      else {
        BoltLog.Warn("Received incorrect port mapping result from {0}", device);
      }
    }

    static void NatUtility_ClosePort(NatDeviceState device, int port) {
      lock (syncLock) {
        Mapping mapping = new Mapping(Protocol.Udp, port, port);
        device.Nat.BeginDeletePortMap(mapping, ar => {
          lock (syncLock) {
            try {
              device.Nat.EndDeletePortMap(ar);
              ClosePortMapping(device, port);
            }
            catch (MappingException exn) {
              if (exn.ErrorCode == 714) {
                ClosePortMapping(device, port);
              }
              else {
                BoltLog.Exception(exn);
              }
            }
            catch (Exception exn) {
              BoltLog.Exception(exn);
            }
          }
        }, null);
      }
    }

  }
}
#endif