using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Matchplay.Shared
{
    /// <summary>
    /// Basic launch command processor (Multiplay prefers passing IP and port along)
    /// </summary>
    public class ApplicationData
    {
        /// <summary>
        /// Commands Dictionary
        /// Supports flags and single variable args (eg. '-argument', '-variableArg variable')
        /// </summary>
        Dictionary<string, Action<string>> m_CommandDictionary = new Dictionary<string, Action<string>>();
        const string k_IPCmd = "ip";
        const string k_PortCmd = "port";
        const string k_QueryPortCmd = "queryPort";
        static bool s_IsServerMode;

        public static bool IsBuildServerMode()
        {
            return s_IsServerMode;
        }

        public static string IP()
        {
            return PlayerPrefs.GetString(k_IPCmd);
        }

        public static int Port()
        {
            return PlayerPrefs.GetInt(k_PortCmd);
        }

        public static int QPort()
        {
            return PlayerPrefs.GetInt(k_QueryPortCmd);
        }

        //Ensure this gets instantiated Early on
        public ApplicationData(bool isServerMode)
        {
            SetIP("127.0.0.1");
            SetPort("7777");
            SetQueryPort("7787");
            m_CommandDictionary["-" + k_IPCmd] = SetIP;
            m_CommandDictionary["-" + k_PortCmd] = SetPort;
            m_CommandDictionary["-" + k_QueryPortCmd] = SetQueryPort;
            s_IsServerMode = isServerMode;
            ProcessCommandLinearguments(Environment.GetCommandLineArgs());
        }

        void ProcessCommandLinearguments(string[] args)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Evaluating Args: ");
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                var nextArg = "";
                if (i + 1 < args.Length) // if we are evaluating the last item in the array, it must be a flag
                    nextArg = args[i + 1];

                sb.Append(arg);
                sb.Append(" : ");
                sb.Append(nextArg);
                if (EvaluatedArgs(arg, nextArg))
                {
                    sb.Append("- FOUND CMD - ");
                    i++;
                }

                sb.AppendLine();
            }

            Debug.Log(sb);
        }

        /// <summary>
        /// Commands and values come in the args array in pairs, so we
        /// </summary>
        bool EvaluatedArgs(string arg, string nextArg)
        {
            if (!IsCommand(arg))
                return false;
            if (IsCommand(nextArg)) // If you have need for flags, make a seperate dict for those.
            {
                return false;
            }

            m_CommandDictionary[arg].Invoke(nextArg);
            return true;
        }

        void SetIP(string ipArgument)
        {
            PlayerPrefs.SetString(k_IPCmd, ipArgument);
        }

        void SetPort(string portArgument)
        {
            if (int.TryParse(portArgument, out int parsedPort))
            {
                PlayerPrefs.SetInt(k_PortCmd, parsedPort);
            }
            else
            {
                Debug.LogError($"{portArgument} does not contain a parseable port!");
            }
        }

        void SetQueryPort(string qPortArgument)
        {
            if (int.TryParse(qPortArgument, out int parsedQPort))
            {
                PlayerPrefs.SetInt(k_QueryPortCmd, parsedQPort);
            }
            else
            {
                Debug.LogError($"{qPortArgument} does not contain a parseable query port!");
            }
        }

        bool IsCommand(string arg)
        {
            return !string.IsNullOrEmpty(arg) && m_CommandDictionary.ContainsKey(arg) && arg.StartsWith("-");
        }
    }
}