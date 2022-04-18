using System;
using System.Threading.Tasks;
using Matchplay.Shared;
using Unity.Services.Core;
using UnityEngine;

namespace Matchplay.Server
{
    /// <summary>
    /// Monobehaviour Singleton pattern for easy access to the Server Game Manager
    /// We seperated the logic away from the Monobehaviour, so we could more easily write tests for it.
    /// </summary>
    public class ServerSingleton : MonoBehaviour
    {
        public static ServerSingleton Instance
        {
            get
            {
                if (s_ServerSingleton != null) return s_ServerSingleton;
                s_ServerSingleton = FindObjectOfType<ServerSingleton>();
                if (s_ServerSingleton == null)
                {
                    Debug.LogError("No ClientSingleton in scene, did you run this from the bootStrap scene?");
                    return null;
                }

                return s_ServerSingleton;
            }
        }

        static ServerSingleton s_ServerSingleton;

        public ServerGameManager Manager
        {
            get
            {
                if (m_GameManager != null)
                {
                    return m_GameManager;
                }

                Debug.LogError($"Server Manager is missing, did you run StartServer?");
                return null;
            }
        }

        ServerGameManager m_GameManager;

        public async Task StartServer()
        {
            await UnityServices.InitializeAsync();
            m_GameManager = new ServerGameManager(
                ApplicationData.IP(),
                ApplicationData.Port(),
                ApplicationData.QPort(),
                new MatchplayNetworkServer(),
                new MatchplayAllocationService());
        }

        // Start is called before the first frame update
        void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            Manager.Dispose();
        }
    }
}