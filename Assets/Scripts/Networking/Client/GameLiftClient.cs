/**
 * GameLiftClient.cs
 * Created by: João Borks [joao.borks@gmail.com]
 * Created on: 2/24/2021 (en-US)
 */
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon;
using Amazon.GameLift;
using Amazon.GameLift.Model;
using Mirror;
using TMPro;
using System.Text.RegularExpressions;
using static UnityEngine.Analytics.IAnalytic;
using System.IO;
using UnityEditor.Rendering;
using UnityEngine.SocialPlatforms;

public class GameLiftClient : MonoBehaviour
{
  private Dictionary<string, Dictionary<string, string>> iniData = new Dictionary<string, Dictionary<string, string>>();

  static string ipPattern = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";

  public TMP_Text latency;
  public TMP_Text fps;
  public TMP_InputField inputAddressField;
  public GameObject loginObject;
  public GameObject logoutObject;

  public string PlayerSessionId => currentPlayerSession.PlayerSessionId;
  public string PlayerId => playerId;

  [SerializeField]
  bool local;
  [SerializeField]
  string aws_ak = "ak";
  [SerializeField]
  string aws_sk = "sk";
  [SerializeField]
  string fleet_name = "fleet-123";

  AmazonGameLiftClient client;
  PlayerSession currentPlayerSession;
  NetworkManager networkManager;
  string playerId;

  void Awake()
  {
    NetworkTime.PingInterval = 5;
    networkManager = FindFirstObjectByType<NetworkManager>();
  }

  void Start()
  {
    Debug.Log("client");

    LoadIniFile("Config.ini");
    Dictionary<string, string> localConfig = iniData["aws"];
    bool is_local = bool.Parse(localConfig["local"]);
    string aws_ak_v = localConfig["aws_ak_v"];
    string aws_sk_v = localConfig["aws_sk_v"];
    string aws_gamelift_feet_name = localConfig["aws_gamelift_feet_name"];
    Debug.Log($"{is_local}, {aws_ak_v}, {aws_sk_v}, {aws_gamelift_feet_name}");

    local = is_local;
    aws_ak = aws_ak_v;
    aws_sk = aws_sk_v;
    fleet_name = aws_gamelift_feet_name;

    UIUpdate(true);
  }

  void FixedUpdate()
  {
    double rtt = NetworkTime.rtt;
    latency.text = "rtt:" + Math.Round(rtt*100, 2);

  }

  void UIUpdate(bool status)
  {
    loginObject.SetActive(status);
    logoutObject.SetActive(!status);
  }

  public void OnJoinBtn()
  {
    string gameIp = inputAddressField.text;
    if (Regex.IsMatch(gameIp, ipPattern))
    {
      Quickplay(gameIp);
    }
    else if (gameIp.Equals("localhost") || string.IsNullOrEmpty(gameIp))
    {
      Quickplay("127.0.0.1");
    }
    else
    {
      Debug.Log("Cannot Join!!!");
    }
  }

  public void OnLeaveBtn()
  {
    UIUpdate(true);
    networkManager.OnStopClient();
  }

  async void Quickplay(string gameIp)
  {
    var fleets = new List<string>();
    var remote_fleed_id = "";
    if (gameIp.Equals("127.0.0.1"))
    {
      local = true;
    }
    else
    {
      local = false;
    }

    var config = new AmazonGameLiftConfig();
    if (local)
      config.ServiceURL = "http://localhost:7778";
    else
      config.RegionEndpoint = RegionEndpoint.USWest2;

    client = new AmazonGameLiftClient(aws_ak, aws_sk, config);
    playerId = Guid.NewGuid().ToString();

    if (!local)
    {
      fleets = await GetFleets();
      Debug.Log($"Found {fleets.Count} active Fleets");
      if (fleets.Count <= 0)
        return;
      remote_fleed_id = fleets.Find(f => f == fleet_name);
      Debug.Log($"fleet id {remote_fleed_id}");
    }
    var sessions = await GetActiveGameSessionsAsync(local ? fleet_name : remote_fleed_id);
    Debug.Log($"Found {sessions.Count} active Game Sessions");
    if (sessions.Count <= 0)
      return;

    string sessionId = "";
    foreach (var s in sessions)
    {
      //Debug.Log(gameIp);
      //Debug.Log(s.IpAddress);
      if (s.Status == GameSessionStatus.ACTIVE && s.IpAddress == gameIp)
      {
        UIUpdate(false);
        sessionId = s.GameSessionId;
        currentPlayerSession = await CreatePlayerSessionAsync(sessionId);
        Debug.Log($"Successfully connected to session {currentPlayerSession.GameSessionId} at [{currentPlayerSession.DnsName}] {currentPlayerSession.IpAddress}:{currentPlayerSession.Port}");
        networkManager.networkAddress = currentPlayerSession.IpAddress;
        networkManager.StartClient();
      }
    }
  }

  async Task<List<string>> GetFleets(CancellationToken token = default)
  {
    var response = await client.ListFleetsAsync(new ListFleetsRequest(), token);
    return response.FleetIds;
  }

  async Task<List<GameSession>> GetActiveGameSessionsAsync(string fleetId, CancellationToken token = default)
  {
    var response = await client.DescribeGameSessionsAsync(new DescribeGameSessionsRequest()
    {
      FleetId = fleetId
    }, token);
    return response.GameSessions;
  }

  async Task<PlayerSession> CreatePlayerSessionAsync(string gameSessionId, CancellationToken token = default)
  {
    var response = await client.CreatePlayerSessionAsync(gameSessionId, playerId, token);
    return response.PlayerSession;
  }

  void LoadIniFile(string filename)
  {
#if UNITY_EDITOR
    string configFilePath = Path.Combine(Application.streamingAssetsPath, filename);
#else
    string configFilePath = Path.Combine ("./", filename);
#endif
    Debug.Log(configFilePath);

    iniData.Clear();

    if (File.Exists(configFilePath))
    {
      string[] lines = File.ReadAllLines(configFilePath);

      string currentSection = null;
      Dictionary<string, string> currentSectionData = null;

      foreach (string line in lines)
      {
        string trimmedLine = line.Trim();

        if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
        {
          currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
          currentSectionData = new Dictionary<string, string>();
          iniData[currentSection] = currentSectionData;
        }
        else if (!string.IsNullOrWhiteSpace(trimmedLine) && currentSectionData != null)
        {
          int equalIndex = trimmedLine.IndexOf('=');
          if (equalIndex >= 0)
          {
            string key = trimmedLine.Substring(0, equalIndex).Trim();
            string value = trimmedLine.Substring(equalIndex + 1).Trim();
            currentSectionData[key] = value;
          }
        }
      }
      Debug.Log("配置加载完成" + configFilePath);
    }
    else
    {
      Debug.LogError("Config file not found: " + configFilePath);
    }
  }

}