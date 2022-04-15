using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Matchplay.Shared;
using Matchplay.Shared.Tools;
using Random = UnityEngine.Random;

namespace Matchplay.Server
{
    public class ServerGameManager : IDisposable
    {
        public MatchplayNetworkServer networkServer => m_NetworkServer;

        MatchplayNetworkServer m_NetworkServer;
        MatchplayBackfiller m_Backfiller;
        string m_ConnectionString => $"{m_ServerIP}:{m_ServerPort}";
        string m_ServerIP = "0.0.0.0";
        int m_ServerPort = 7777;
        int m_QueryPort = 7787;
        const int k_MultiplayServiceTimeout = 15000;
        bool m_LocalServer;
        MatchplayAllocationService m_MatchplayAllocationService;
        SynchedServerData m_SynchedServerData;

    /// <summary>
        /// Attempts to initialize the server with services (If we are on Multiplay) and if we time out, we move on to default setup for local testing.
        /// </summary>
        public async Task BeginServerAsync()
        {
            m_NetworkServer = new MatchplayNetworkServer();
            m_MatchplayAllocationService = new MatchplayAllocationService();
            m_ServerIP = ApplicationData.IP();
            m_ServerPort = ApplicationData.Port();
            m_QueryPort = ApplicationData.QPort();

            var startingGameInfo = new GameInfo
            {
                gameMode = GameMode.Staring,
                map = Map.Lab,
                gameQueue = GameQueue.Casual
            };

            var matchmakerPayloadTask = m_MatchplayAllocationService.BeginServerAndAwaitMatchmakerAllocation();

            try
            {
                //Try to get the matchmaker allocation payload from the multiplay services, and init the services if we do.
                if (await Task.WhenAny(matchmakerPayloadTask, Task.Delay(k_MultiplayServiceTimeout)) == matchmakerPayloadTask)
                {
                    Debug.Log($"Got allocation: {matchmakerPayloadTask.Result}");
                    if (matchmakerPayloadTask.Result != null)
                    {
                        var matchmakerPayload = matchmakerPayloadTask.Result;
                        startingGameInfo = PickSharedGameInfo(matchmakerPayload);

                        await m_MatchplayAllocationService.BeginServerCheck(startingGameInfo);
                        m_MatchplayAllocationService.SetPlayerCount((ushort)matchmakerPayload.MatchProperties.Players.Count);

                        m_NetworkServer.OnPlayerJoined += UserJoinedServer;
                        m_NetworkServer.OnPlayerLeft += UserLeft;

                        m_Backfiller = new MatchplayBackfiller(m_ConnectionString, matchmakerPayload.QueueName, matchmakerPayload.MatchProperties, startingGameInfo.MaxUsers);

                        if (m_Backfiller.NeedsPlayers())
                        {
                            await m_Backfiller.CreateNewbackfillTicket();
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Connecting to Multiplay Timed out, starting with defaults.");
                    m_LocalServer = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Something went wrong trying to set up the Services:\n{ex} ");
            }

            m_SynchedServerData = await m_NetworkServer.StartServer(m_ServerIP, m_ServerPort, startingGameInfo); //Use Network transforms on the chairs/players to sync positions
            m_SynchedServerData.map.OnValueChanged += OnServerChangedMap;
            m_SynchedServerData.gameMode.OnValueChanged += OnServerChangedMode;
        }
#region ServerSynching
        //There are three data locations that need to be kept in sync, Game Server, Backfill Match Ticket, and the Multiplay Server
        //The Netcode Game Server is the source of truth, and we need to propagate the state of it to the multiplay server.
        //For the matchmaking ticket, it should already have knowledge of the players, unless a player joined outside of matchmaking.

        //For now we don't have any mechanics to change the map or mode mid-game. But if we did, we would update the backfill ticket to reflect that too.
        void OnServerChangedMap(Map oldMap, Map newMap)
        {
            m_MatchplayAllocationService.ChangedMap(newMap);
        }

        void OnServerChangedMode(GameMode oldMode, GameMode newMode)
        {
            m_MatchplayAllocationService.ChangedMode(newMode);
        }

        void UserJoinedServer(UserData joinedUser)
        {
            m_Backfiller.AddPlayerToMatch(joinedUser);
            m_MatchplayAllocationService.AddPlayer();
            if (!m_Backfiller.NeedsPlayers() && m_Backfiller.Backfilling)
                Task.Run(() => m_Backfiller.StopBackfill());
        }

        void UserLeft(UserData leftUser)
        {
            m_Backfiller.RemovePlayerFromMatch(leftUser.userAuthId);
            m_MatchplayAllocationService.RemovePlayer();
            if (m_Backfiller.NeedsPlayers() && !m_Backfiller.Backfilling)
                Task.Run(() => m_Backfiller.CreateNewbackfillTicket());
        }
#endregion
        /// <summary>
        /// Take the list of players and find the most popular game preferences and run the server with those
        /// </summary>
        public static GameInfo PickSharedGameInfo(MatchmakerAllocationPayload mmAllocation)
        {
            var mapCounter = new Dictionary<Map, int>();
            var modeCounter = new Dictionary<GameMode, int>();

            //Gather all the modes all the players have selected
            foreach (var player in mmAllocation.MatchProperties.Players)
            {
                var playerGameInfo = player.CustomData.GetAs<GameInfo>();

                //Since we are using flags, each player might have more than one map selected.
                foreach (var map in playerGameInfo.map.GetUniqueFlags())
                {
                    if (mapCounter.ContainsKey(map))
                        mapCounter[map] += 1;
                    else
                        mapCounter[map] = 1;
                }

                foreach (var mode in playerGameInfo.gameMode.GetUniqueFlags())
                    if (modeCounter.ContainsKey(mode))
                        modeCounter[mode] += 1;
                    else
                        modeCounter[mode] = 1;
            }

            Map mostPopularMap = Map.None;
            int highestCount = 0;

            foreach (var (map, count) in mapCounter)
            {
                //Flip a coin for equally popular maps
                if (count == highestCount)
                {
                    if (Random.Range(0, 2) != 0)
                        mostPopularMap = map;
                    continue;
                }

                if (count > highestCount)
                {
                    mostPopularMap = map;
                    highestCount = count;
                }
            }

            GameMode mostPopularMode = GameMode.None;
            highestCount = 0;
            foreach (var (gameMode, count) in modeCounter)
            {
                //Flip a coin for equally popular maps
                if (count == highestCount)
                {
                    if (Random.Range(0, 2) != 0)
                        mostPopularMode = gameMode;
                    continue;
                }

                if (count > highestCount)
                {
                    mostPopularMode = gameMode;
                    highestCount = count;
                }
            }

            //Convert from the multiplay queue values to local enums
            var queue = GameInfo.ToGameQueue(mmAllocation.QueueName);

            return new GameInfo { map = mostPopularMap, gameMode = mostPopularMode, gameQueue = queue };
        }

        public void Dispose()
        {
            if (!m_LocalServer)
            {
                if (m_NetworkServer.OnPlayerJoined != null) m_NetworkServer.OnPlayerJoined -= UserJoinedServer;
                if (m_NetworkServer.OnPlayerLeft != null) m_NetworkServer.OnPlayerLeft -= UserLeft;
            }

            if (m_SynchedServerData != null)
            {
                if (m_SynchedServerData.map.OnValueChanged != null) m_SynchedServerData.map.OnValueChanged -= OnServerChangedMap;
                if (m_SynchedServerData.gameMode.OnValueChanged != null) m_SynchedServerData.gameMode.OnValueChanged -= OnServerChangedMode;
            }

            m_Backfiller?.Dispose();
            m_MatchplayAllocationService?.Dispose();
            networkServer?.Dispose();
        }
    }
}
