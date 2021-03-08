using System;
using System.Collections.Generic;
using UnityEngine;
using Aws.GameLift.Server;
using Newtonsoft.Json;
using System.Text;

public class BADNetworkServer : MonoBehaviour
{
   private static int MaxMessageSize = 1024;
   private static int MaxPlayersPerSession = 2;
   private Telepathy.Server _server = new Telepathy.Server(MaxMessageSize);
   private Dictionary<int, string> _playerSessions;
   private GameLiftServer _gameLiftServer;
   public string GameSessionState = "";
   public string GameOverState = "GAME_OVER";

   private void OnDataReceived(int connectionId, ArraySegment<byte> message)
   {
      Debug.Log("Data received from connectionId: " + connectionId);

      byte[] messageBytes = message.Array;
      string convertedMessage = Encoding.UTF8.GetString(messageBytes, 0, messageBytes.Length);
      BADNetworkMessage networkMessage = JsonConvert.DeserializeObject<BADNetworkMessage>(convertedMessage);

      ProcessMessage(connectionId, networkMessage);
   }

   private void ProcessMessage(int connectionId, BADNetworkMessage networkMessage)
   {
      Debug.Log("Network message: " + networkMessage);

      if (networkMessage != null && networkMessage._opCode != null)
      {
         Debug.Log("processing opcode");

         if (networkMessage._opCode == "CONNECT")
         {
            Debug.Log("CONNECT OP CODE HIT");
            HandleConnect(connectionId, networkMessage._playerSessionId);

            // send response
            BADNetworkMessage responseMessage = new BADNetworkMessage("CONNECTED", networkMessage._playerSessionId);
            SendMessage(connectionId, responseMessage);

            CheckAndSendGameReadyToStartMsg(connectionId);
         }
         // TODO: handle more opCodes here for game play stuff
      }
      else
      {
         Debug.Log("ProcessMessage: empty message or null opCode, message ignored.");
      }
   }

   public void SendMessage(int connectionId, BADNetworkMessage networkMessage)
   {
      var data = JsonConvert.SerializeObject(networkMessage);
      var encoded = Encoding.UTF8.GetBytes(data);
      var asWriteBuffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);

      Debug.Log("send message to playerSessionId: " + networkMessage._playerSessionId + ", with connId: " + connectionId);
      _server.Send(connectionId, asWriteBuffer);
   }

   private void CheckAndSendGameReadyToStartMsg(int connectionId)
   {
      if (_playerSessions.Count == MaxPlayersPerSession)
      {
         Debug.Log("Game is full and is ready to start.");

         // tell all players the game is ready to start
         foreach (KeyValuePair<int, string> playerSession in _playerSessions)
         {
            BADNetworkMessage responseMessage = new BADNetworkMessage("START", playerSession.Value);
            SendMessage(playerSession.Key, responseMessage);
         }
      }
   }

   public bool HandleConnect(int connectionId, string playerSessionId)
   {
      Debug.Log("HandleConnect");

      var outcome = GameLiftServerAPI.AcceptPlayerSession(playerSessionId);
      if (outcome.Success)
      {
         Debug.Log("PLAYER SESSION VALIDATED");
      }
      else
      {
         Debug.Log("PLAYER SESSION REJECTED. AcceptPlayerSession() returned " + outcome.Error.ToString());
      }

      // track our player sessions
      _playerSessions.Add(connectionId, playerSessionId);

      return outcome.Success;
   }

   // For the sake of simplicity for this demo, if any player disconnects, just end the game. 
   // That means if only one player joins, then disconnects, the game session ends.
   // Your game may remain open to receiving new players, without ending the game session, up to you.
   private void CheckForGameEnd(int disconnectingId)
   {
      Debug.Log("CheckForGameEnd");

      // TODO: also probably check state of game here or something?

      if (GameSessionState != GameOverState)
      {
         GameSessionState = GameOverState;

         // For this demo game, just disconnect everyone else in the session when one player disconnects. 
         // An all or nothing type of game. And at this point, since the game session will be ending, we don't 
         // need to worry about removing playerSessions from the _playerSessions Dictonary.
         foreach (KeyValuePair<int, string> playerSession in _playerSessions)
         {
            // disconnect all other clients
            if (playerSession.Key != disconnectingId)
            {
               _server.Disconnect(playerSession.Key);
            }

            _gameLiftServer.HandleDisconnect(playerSession.Value); // player session id
         }

         Debug.Log("Ending game, player disconnected.");
         _gameLiftServer.HandleGameEnd();
      }
      else
      {
         Debug.Log("Game session is already being shut down.");
      }
   }

   private void OnDisonnected(int connectionId)
   {
      Debug.Log("Connection ID: " + connectionId + " Disconnected.");

      CheckForGameEnd(connectionId);
   }

   private void OnConnected(int connectionId)
   {
      Debug.Log("Connection ID: " + connectionId + " Connected");
   }

   public void StartTCPServer()
   {
      // had to set these to 0 or else the TCP connection would timeout after the default 5 seconds.  Investivate further.
      _server.SendTimeout = 0;
      _server.ReceiveTimeout = 0;

      _server.Start(GameLiftServer.TcpServerPort);
   }

   void Awake()
   {
      _playerSessions = new Dictionary<int, string>();

      _gameLiftServer = GetComponent<GameLiftServer>();

      Application.runInBackground = true;

      _server.OnConnected = OnConnected;
      _server.OnData = OnDataReceived;
      _server.OnDisconnected = OnDisonnected;
   }

   void Update()
   {
      // tick to process messages, (even if not active so we still process disconnect messages)
      _server.Tick(100);
   }

   void OnApplicationQuit()
   {
      Debug.Log("BADNetworkServer.OnApplicationQuit");
      _server.Stop();
   }
}
