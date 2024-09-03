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

public class GameLiftClient : MonoBehaviour
{
  static string ipPattern = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
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
    networkManager = FindFirstObjectByType<NetworkManager>();
  }

  void Start()
  {
    Debug.Log("client");
    UIUpdate(true);
    var config = new AmazonGameLiftConfig();
    if (local)
      config.ServiceURL = "http://localhost:7778";
    else
      config.RegionEndpoint = RegionEndpoint.USWest2;

    client = new AmazonGameLiftClient(aws_ak, aws_sk, config);
    playerId = Guid.NewGuid().ToString();

    //Quickplay();
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
      Debug.Log(gameIp);
      Debug.Log(s.IpAddress);
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
    //var sessionId = sessions.FirstOrDefault(s => {
    //  Debug.Log(gameIp);
    //  Debug.Log(s.IpAddress);
    //  return s.Status == GameSessionStatus.ACTIVE && s.IpAddress == gameIp;
    //}).GameSessionId;

    //currentPlayerSession = await CreatePlayerSessionAsync(sessionId);
    //Debug.Log($"Successfully connected to session {currentPlayerSession.GameSessionId} at [{currentPlayerSession.DnsName}] {currentPlayerSession.IpAddress}:{currentPlayerSession.Port}");
    //networkManager.networkAddress = currentPlayerSession.IpAddress;
    //networkManager.StartClient();
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

}