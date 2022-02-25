using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Matchplay.Shared;
using Matchplay.Infrastructure;
using Matchplay.Tools;

namespace Matchplay.Server
{
    public class ServerGameManager : IDisposable
    {
        string m_ServerIP = "0.0.0.0";
        int m_ServerPort = 7777;
        int m_QueryPort = 7787;

        MatchplayGameInfo m_defaultServerInfo = new MatchplayGameInfo()
        {
            MaxPlayers = 10,
            CurrentGameMode = GameMode.Staring,
            CurrentMap = Map.Lab,
            CurrentGameQueue = GameQueue.Casual
        };

        UnitySqp m_UnitySqp;
        MatchplayServer m_Server;
        ApplicationData m_Data;

        [Inject]
        void InjectDependencies(MatchplayServer server, UnitySqp sqpServer, ApplicationData data)
        {
            m_Data = data;
            m_UnitySqp = sqpServer;
            m_Server = server;
            m_Server.Init();
        }

        public void SetGameMode(GameMode toGameMode)
        {
            m_defaultServerInfo.CurrentGameMode = toGameMode;
        }

        public void ChangeMap(Map toMap)
        {
            m_defaultServerInfo.CurrentMap = toMap;
            var sceneString = ToScene(m_defaultServerInfo.CurrentMap);
            if (string.IsNullOrEmpty(sceneString))
            {
                Debug.LogError($"Cant Change map, no valid map selection in {toMap}.");
            }

            NetworkManager.Singleton.SceneManager.LoadScene(ToScene(m_defaultServerInfo.CurrentMap), LoadSceneMode.Single);
        }

        public void BeginServer()
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            m_ServerIP = m_Data.IP();
            m_ServerPort = m_Data.Port();
            m_QueryPort = m_Data.QPort();
            m_UnitySqp.StartSqp(m_ServerIP, m_ServerPort, m_QueryPort, m_defaultServerInfo);
            m_Server.StartServer(m_ServerIP, m_ServerPort);
        }

        /// <summary>
        /// Convert the map flag enum to a scene name.
        /// </summary>
        string ToScene(Map maps)
        {
            var mapSelection = new List<Map>(maps.GetUniqueFlags());
            if (mapSelection.Count < 1)
            {
                return "";
            }

            var topMap = mapSelection.First();
            if (topMap.HasFlag(Map.Lab))
            {
                return "game_lab";
            }

            if (topMap.HasFlag(Map.Space))
            {
                return "game_space";
            }

            return "";
        }

        void Start() { }

        void OnServerStarted()
        {
            ChangeMap(Map.Lab);
        }

        public void Dispose()
        {
            if (NetworkManager.Singleton == null)
                return;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }
    }
}
