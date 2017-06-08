using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DLog : MonoBehaviour {

    public static bool disable = true;
    public static int logLevel = 0;

    public void Awake() {
        DontDestroyOnLoad(this.gameObject);
    }

    public void Update() {
        if(Input.GetKeyDown(KeyCode.L)) {
            disable = !disable;
            Debug.Log("DLog is disabled: " + disable);
        }
        if(Input.GetKeyDown(KeyCode.Alpha0)) {
            logLevel = 0;
        }
        if(Input.GetKeyDown(KeyCode.Alpha1)) {
            logLevel = 1;
        }
        if(Input.GetKeyDown(KeyCode.Alpha2)) {
            logLevel = 2;
        }
        if(Input.GetKeyDown(KeyCode.Alpha3)) {
            logLevel = 3;
        }
        if(Input.GetKeyDown(KeyCode.Alpha4)) {
            logLevel = 4;
        }
        if(Input.GetKeyDown(KeyCode.Alpha5)) {
            logLevel = 5;
        }
    }

    //public static void Log(string msg) {
    //    if(disable) return;
    //    if(logLevel == 0) return;
    //    Debug.Log(msg);
    //}

    //separate log levels so we can only log certain parts of code
    //if we set DLog.logLevel to 4, only DLog.Log('msg', 4)'s will be called.
    public static void Log(string msg, int logLevel = 0) {
        if(disable) return;
        if(DLog.logLevel == logLevel) {
            Debug.Log(msg);
        }
    }

    public static void LogF(string format, params object[] msgs) {
        if(disable) return;
        Debug.Log(string.Format(format, msgs));
    }
}
