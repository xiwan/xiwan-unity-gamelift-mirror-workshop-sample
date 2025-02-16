using System;
using Amazon;
using UnityEngine;

public class UtilsKlass
{
  public static readonly RegionEndpoint awsRegion = RegionEndpoint.USWest2;
  public static readonly String bucketName = "gamelift-tankserver-demo";
  public static readonly String keyName = "tankServer";

  public static string buildLogJson(string gameSessionId, string logString, string stackTrace, LogType type)
  {
    return "{\"type\":\"" + type + "\",\"time\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff") + "\",\"session\":\"" + gameSessionId + "\",\"message\":\"" + logString + "\",\"stackTrace\":\"" + stackTrace + "\"}";
  }


  public static string GetArg(string name)
  {
    var args = System.Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length; i++)
    {
      if (args[i] == name && args.Length > i + 1)
      {
        return args[i + 1];
      }
    }
    return null;
  }

}

