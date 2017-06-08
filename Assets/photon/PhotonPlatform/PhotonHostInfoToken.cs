using System;
using System.Collections;
using System.Collections.Generic;
using UdpKit;
using UnityEngine;

[Obsolete("Use the new IBoltPhotonCloudRoomProperties interface on a custom protocol token class to supply room properties instead")]
public class PhotonHostInfoToken : Bolt.IProtocolToken {
  public ExitGames.Client.Photon.Hashtable CustomRoomProperties = new ExitGames.Client.Photon.Hashtable();
  public void Read(UdpPacket packet) { }
  public void Write(UdpPacket packet) { }
}
