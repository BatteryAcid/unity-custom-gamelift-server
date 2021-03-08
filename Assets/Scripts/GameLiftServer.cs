using System;
using System.Collections.Generic;
using UnityEngine;
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;

// Based on https://docs.aws.amazon.com/gamelift/latest/developerguide/integration-engines-unity-using.html
public class GameLiftServer : MonoBehaviour
{
   // server used to communicate with client
   private BADNetworkServer _server;

   // Identify port number (hard coded here for simplicity) the game server is listening on for player connections
   public static int TcpServerPort = 7777;

   // This is an example of a simple integration with GameLift server SDK that will make game server processes go active on GameLift!
   public void Start()
   {
      //InitSDK will establish a local connection with GameLift's agent to enable further communication.
      var initSDKOutcome = GameLiftServerAPI.InitSDK();

      if (initSDKOutcome.Success)
      {
         ProcessParameters processParameters = new ProcessParameters(
            this.OnGameSession,
            this.OnGameSessionUpdate,
            this.OnProcessTerminate,
            this.OnHealthCheck,
            TcpServerPort, // This game server tells GameLift the port it will listen on for incoming player connections.
            new LogParameters(new List<string>()
            {
               // Here, the game server tells GameLift what set of files to upload when the game session ends.
               // GameLift will upload everything specified here for the developers to fetch later.
               
               // When -isProd is NOT set, use a path relevant for local testing
               Startup.IsArgFlagPresent("-isProd") ? "/local/game/logs/server.log" : "~/Library/Logs/Unity/server.log"
            }
         ));

         // Calling ProcessReady tells GameLift this game server is ready to receive incoming game sessions!
         var processReadyOutcome = GameLiftServerAPI.ProcessReady(processParameters);
         if (processReadyOutcome.Success)
         {
            print("ProcessReady success.");

            _server = GetComponent<BADNetworkServer>();

            if (_server != null)
            {
               Debug.Log("BADNetworkServer is good.");
               _server.StartTCPServer();
            }
            else
            {
               Debug.Log("BADNetworkServer is null.");
            }
         }
         else
         {
            print("ProcessReady failure : " + processReadyOutcome.Error.ToString());
         }
      }
      else
      {
         print("InitSDK failure : " + initSDKOutcome.Error.ToString());
      }
   }

   void OnGameSession(GameSession gameSession)
   {
      // When a game session is created, GameLift sends an activation request to the game server and passes along 
      // the game session object containing game properties and other settings. Here is where a game server should 
      // take action based on the game session object. Once the game server is ready to receive incoming player 
      // connections, it should invoke GameLiftServerAPI.ActivateGameSession()
      Debug.Log("ActivateGameSession");
      GameLiftServerAPI.ActivateGameSession();
   }

   void OnProcessTerminate()
   {
      // OnProcessTerminate callback. GameLift will invoke this callback before shutting down an instance hosting this game server.
      // It gives this game server a chance to save its state, communicate with services, etc., before being shut down.

      // From the Docs: https://docs.aws.amazon.com/gamelift/latest/developerguide/integration-server-sdk-csharp-ref-actions.html#integration-server-sdk-csharp-ref-getterm
      // GameLift may call onProcessTerminate() for the following reasons: (1) for poor health (the server process has 
      // reported port health or has not responded to GameLift, (2) when terminating the instance during a scale-down event, 
      // or (3) when an instance is being terminated due to a spot-instance interruption.
      Debug.Log("OnProcessTerminate");

      FinalizeServerProcessShutdown();
   }

   void OnGameSessionUpdate(UpdateGameSession updateGameSession)
   {
      // When a game session is updated (e.g. by FlexMatch backfill), GameLiftsends a request to the game
      // server containing the updated game session object.  The game server can then examine the provided
      // matchmakerData and handle new incoming players appropriately.
      // updateReason is the reason this update is being supplied.
      Debug.Log("updateGameSession");
   }

   bool OnHealthCheck()
   {
      // This is the HealthCheck callback. GameLift will invoke this callback every 60 seconds or so.
      // Here, a game server might want to check the health of dependencies and such. Simply return true if 
      // healthy, false otherwise. The game server has 60 seconds to respond with its health status. GameLift 
      // will default to 'false' if the game server doesn't respond in time. In this case, we're always healthy!
      return true;
   }

   public void HandleDisconnect(string playerSessionId)
   {
      Debug.Log("HandleDisconnect for player session id: " + playerSessionId);

      try
      {
         // Remove players from the game session that disconnected
         var outcome = GameLiftServerAPI.RemovePlayerSession(playerSessionId);

         if (outcome.Success)
         {
            Debug.Log("PLAYER SESSION REMOVED");
         }
         else
         {
            Debug.Log("PLAYER SESSION REMOVE FAILED. RemovePlayerSession() returned " + outcome.Error.ToString());
         }
      }
      catch (Exception e)
      {
         Debug.Log("PLAYER SESSION REMOVE FAILED. RemovePlayerSession() exception " + Environment.NewLine + e.Message);
         throw;
      }
   }

   private void FinalizeServerProcessShutdown()
   {
      Debug.Log("GameLiftServer.FinalizeServerProcessShutdown");

      // All game session clean up should be performed before this, as it should be the last thing that
      // is called when terminating a game session. After a successful outcome from ProcessEnding, make 
      // sure to call Application.Quit(), otherwise the application does not shutdown properly. see:
      // https://forums.awsgametech.com/t/server-process-exited-without-calling-processending/5762/17

      var outcome = GameLiftServerAPI.ProcessEnding();
      if (outcome.Success)
      {
         Debug.Log("FinalizeServerProcessShutdown: GAME SESSION TERMINATED");
         Application.Quit();
      }
      else
      {
         Debug.Log("FinalizeServerProcessShutdown: GAME SESSION TERMINATION FAILED. ProcessEnding() returned " + outcome.Error.ToString());
      }
   }

   public void HandleGameEnd()
   {
      Debug.Log("HandleGameEnd");

      FinalizeServerProcessShutdown();
   }

   // a Unity callback when the program is quitting
   void OnApplicationQuit()
   {
      Debug.Log("GameLiftServer.OnApplicationQuit");

      FinalizeServerProcessShutdown();
   }
}
