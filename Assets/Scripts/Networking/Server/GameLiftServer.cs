using UnityEngine;
using Mirror;
using Aws.GameLift.Server;
using System.Collections.Generic;
using kcp2k;
using Aws.GameLift;
using Aws.GameLift.Server.Model;
using Amazon.S3;
using System;
using System.IO;
using System.Collections;
using Amazon.Runtime;
using Amazon.S3.Transfer;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using System.Threading.Tasks;
using System.Data;
using Unity.Burst.Intrinsics;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

public class GameLiftServer : MonoBehaviour
{
  // If true, we know the process is terminating.
  private bool processEndingCalled = false;

  NetworkManager networkManager;

  ushort gamePort = 8000;
  ushort kcpPort = 7000;
  float duration = 0;
  string _logFileName = "gameserver";
  string _logFilePath = "";
  string _gameSessionId = "";
  string _bucketName = UtilsKlass.bucketName;
  string _roleName = "arn:aws:iam::942636716027:role/SSMDefaultRoleForPVREReporting";
  string _regionCode = "us-west-2";

  void Awake()
  {

    networkManager = FindFirstObjectByType<NetworkManager>();
    if (!string.IsNullOrEmpty(UtilsKlass.GetArg("-k")))
    {
      kcpPort = ushort.Parse(UtilsKlass.GetArg("-k"));
      networkManager.GetComponent<KcpTransport>().port = kcpPort;
    }
    if (!string.IsNullOrEmpty(UtilsKlass.GetArg("-p")))
    {
      gamePort = ushort.Parse(UtilsKlass.GetArg("-p"));
    }
    if (!string.IsNullOrEmpty(UtilsKlass.GetArg("-log")))
    {
      _logFileName = UtilsKlass.GetArg("-log");
    }
    if (!string.IsNullOrEmpty(UtilsKlass.GetArg("-logFile")))
    {
      _logFileName = UtilsKlass.GetArg("-logFile");
    }
    if (!string.IsNullOrEmpty(UtilsKlass.GetArg("-s3")))
    {
      _bucketName = UtilsKlass.GetArg("-s3");
    }
    if (!string.IsNullOrEmpty(UtilsKlass.GetArg("-role")))
    {
      _roleName = UtilsKlass.GetArg("-role");
    }
    if (!string.IsNullOrEmpty(UtilsKlass.GetArg("-region")))
    {
      _regionCode = UtilsKlass.GetArg("-region");
    }

  }

  void Update()
  {
    
  }

  private void OnApplicationQuit()
  {
    // If application is quitting, make sure GameLift service knows the process is ending.
    CallProcessEnding();

    // Per GameLift docs, destroy server API on quit.
    GameLiftServerAPI.Destroy();
    Debug.Log("Goodbye, cruel world.");
  }

  private void CallProcessEnding()
  {
    if (!processEndingCalled)
    {
      GenericOutcome processEndingOutcome = GameLiftServerAPI.ProcessEnding();
      if (processEndingOutcome.Success)
      {
        processEndingCalled = true;
        Debug.Log("ProcessEnding called successfully.");
      }
      else
      {
        Debug.LogError("ProcessEnding call failed: " + processEndingOutcome.ToString());
      }
    }
  }

  private void OnGameSessionStarted(GameSession gameSession)
  {
    // When ready to receive incoming player connections, invoke "ActivateGameSession."
    GameLiftServerAPI.ActivateGameSession();
    Debug.Log("Game Session started");
  }

  private void OnGameSessionUpdated(UpdateGameSession updateGameSession)
  {
    Debug.Log("Game Session update: " + updateGameSession.UpdateReason);
  }

  private void OnProcessTerminated()
  {
    Debug.Log("OnProcessTerminated called!");
    CallProcessEnding();
    Debug.Log("Application is quitting...");
    Application.Quit();
  }

  private bool OnHealthCheck()
  {
    return true;
  }

  private bool OnValidatePlayerSession(string playerId)
  {
    GenericOutcome acceptPlayerSessionOutcome = GameLiftServerAPI.AcceptPlayerSession(playerId);
    if (acceptPlayerSessionOutcome.Success)
    {
      Debug.Log("Player Session " + playerId + " accepted.");
      return true;
    }
    else
    {
      Debug.LogError("Player Session " + playerId + " rejected: " + acceptPlayerSessionOutcome.Error.ToString());
      return false;
    }
  }

  //private void OnClientDisconnected(ClientInfo clientInfo)
  //{
  //  Debug.Log("Removing player session for " + clientInfo.playerInfo.authToken);
  //  GameLiftServerAPI.RemovePlayerSession(clientInfo.playerInfo.authToken);
  //}

  private void OnPlayEnded()
  {
    Debug.Log("Terminating game session.");
    OnProcessTerminated();
  }

  //This is an example of a simple integration with GameLift server SDK that will make game server processes go active on GameLift!
  void Start()
  {
    //Identify port number (hard coded here for simplicity) the game server is listening on for player connections
    //var gamePort = 7777;

    //InitSDK will establish a local connection with GameLift's agent to enable further communication.
    var initSDKOutcome = GameLiftServerAPI.InitSDK();
    if (!initSDKOutcome.Success)
    {
      Debug.LogError("SDK Init failed: " + initSDKOutcome.Error.ToString());
      //TODO: Maybe Application.Quit()? Not good if this fails!
      return;
    }

    // List of log files we want GameLift to gather.
    // This assumes that we pass "-logFile server.log" as a command line argument when starting the process.
    LogParameters logParams = new LogParameters(new List<string>() {
        $"/local/game/{_logFileName}-{gamePort}.log",
    });

    // Params specifies options and callbacks for this process on GameLift.
    ProcessParameters processParams = new ProcessParameters(
        OnGameSessionStarted,
        OnGameSessionUpdated,
        OnProcessTerminated,
        OnHealthCheck,
        gamePort,
        logParams);

    // Call "ProcessReady" to tell GameLift that this process is ready to host a game session!
    GenericOutcome processReadyOutcome = GameLiftServerAPI.ProcessReady(processParams);
    if (!processReadyOutcome.Success)
    {
      Debug.LogError("SDK ProcessReady failed: " + processReadyOutcome.Error.ToString());
      //TODO: Maybe Application.Quit()? Not good if this fails!
      return;
    }

    _gameSessionId = "gs"+GameLiftServerAPI.GetGameSessionId().Result;

    Debug.Log("Ready to go!");
    var ReadyInfo = $"SERVER GAME-PORT:{gamePort} KCP-PORT: {kcpPort} GAME-SESSION-ID: {_gameSessionId}";
    Debug.Log(ReadyInfo);

    StartCoroutine(OutputLogs(5f));

  }

  private IEnumerator OutputLogs(float interval)
  {
    while (true)
    {
      
      yield return new WaitForSeconds(interval);
      duration += interval;
      if (duration > 60)
      {
        OnProcessTerminated();
        break;
      }

      Debug.Log("Log output every 5 seconds.");
      var logJson = UtilsKlass.buildLogJson(_gameSessionId, "Log output every 5 seconds.", "", LogType.Log);
      Log(logJson);
    }
  }

  public void Log(string logJson)
  {
    var logFileName = $"{_gameSessionId}-{_logFileName}-{gamePort}-{DateTime.Now.ToString("yyyyMMdd-HH")}.log ";

#if UNITY_EDITOR
    _logFilePath = Application.streamingAssetsPath + "/logs/" + logFileName;
#elif UNITY_STANDALONE_LINUX
    _logFilePath = $"/local/game/{_logFileName}-{gamePort}.log";
#elif UNITY_STANDALONE_OSX
    _logFilePath = "./logs/" + logFileName;
#endif
    using (TextWriter writer = new StreamWriter(_logFilePath,true))
    {
      writer.WriteLineAsync(logJson);
    }

    LogS3Async(logFileName);
  }

  public async Task LogS3Async(string logFileName)
  {
    try
    {
      var RegionCode = RegionEndpoint.GetBySystemName(_regionCode);

      // 从凭证配置文件加载凭证
      var client = new AmazonSecurityTokenServiceClient(RegionCode);
      var request = new AssumeRoleRequest
      {
        RoleArn = _roleName,
        RoleSessionName = System.Guid.NewGuid().ToString()
      };

      var response = await client.AssumeRoleAsync(request);
      var s3Client = new AmazonS3Client(response.Credentials, RegionCode);
      var fileTransferUtility = new TransferUtility(s3Client);

      // 上传本地文件到S3
      var fileTransferUtilityRequest = new TransferUtilityUploadRequest
      {
        BucketName = _bucketName,
        FilePath = _logFilePath,
        StorageClass = S3StorageClass.StandardInfrequentAccess,
        PartSize = 6291456, // 6 MB.
        Key = UtilsKlass.keyName + "/" + logFileName
      };
      await fileTransferUtility.UploadAsync(fileTransferUtilityRequest);
      Debug.Log($"File {_logFilePath} uploaded {UtilsKlass.bucketName} successfully!");
    }
    catch (AmazonS3Exception e)
    {
      Debug.LogError($"Error: {e.Message}");
    }
  }

}