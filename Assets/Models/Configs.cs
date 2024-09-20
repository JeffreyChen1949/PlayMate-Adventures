using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Models
{
    public class OpenAIClientOption
    {
        public string Endpoint { get; set; } = "";
        public string ApiKey { get; set; } = "";
    }

    [System.Serializable]
    public class OpenAIConfig
    {
        public string DeploymentName;
    }

    [System.Serializable]
    public class OpenAIClientConfig
    {
        public string Endpoint;
        public string ApiKey;
    }

    [System.Serializable]
    public class Config
    {
        public OpenAIConfig OpenAI;
        public OpenAIClientConfig OpenAIClient;
    }
    public class ConfigManager
    {
        public static Config config;

        public static void LoadConfig()
        {
            string jsonString = @"
        {
            ""OpenAI"": {
                ""DeploymentName"": ""Dalle3""
            },
            ""OpenAIClient"": {
                ""Endpoint"": ""https://*****.openai.azure.com/"",
                ""ApiKey"": ""*******""
            }
        }";

            config = JsonUtility.FromJson<Config>(jsonString);
        }
    }
}
