using Azure.AI.OpenAI.Assistants;
using Azure;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine;
using Azure.AI.OpenAI;
using System.Threading;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;

namespace Assets.Models
{
    public class AzureAssistantClient
    {
        public AssistantsClient client;

        public HttpClient Hclient;

        public string imageUrl = "http://127.0.0.1:5500/fd.jpeg";

        public AzureAssistantClient(string endpoint, string apiKey)
        {
            if (string.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            if (string.IsNullOrEmpty(apiKey)) throw new ArgumentNullException(nameof(apiKey));

            client = new AssistantsClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            Hclient = new HttpClient();
        }

        public async Task<AssistantThread> CreateThreadAsync()
        {
            var response = await client.CreateThreadAsync();
            return response.Value;
        }
        public async Task<string> SubmitToolOutputsAsync(string threadid, string runId, string actionResult)
        {
            // Prepare the payload for the API call
            var submitAction = new SubmitToolOutput
            {
                RunId = runId,
                Outputs = actionResult
            };

            var content = new StringContent(JsonSerializer.Serialize(submitAction), System.Text.Encoding.UTF8, "application/json");

            // Assuming you have an HttpClient set up for making API calls, modify the domain based on your service
            var response = await Hclient.PostAsync($"https://*****.openai.azure.com/openai/threads/{threadid}/runs/{runId}/submit-tool-outputs", content);

            // Ensure the request was successful
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to submit tool outputs. Status Code: {response.StatusCode}");
            }

            // Optionally process the response, if needed
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
            //Console.WriteLine("SubmitToolOutputs Response: " + responseContent);
        }

        public async Task<ThreadMessage> AddMessageToThreadAsync(string threadId, string messageText)
        {
            try
            {
                var response = await client.CreateMessageAsync(
                threadId,
                MessageRole.User,
                messageText
            );

                return response.Value;
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message);
            }
            return null;
        }

        public async Task<ThreadRun> RunThreadAsync(string threadId, string assistantId)
        {
            var response = await client.CreateRunAsync(
                threadId,
                new CreateRunOptions(assistantId)
            );
            return response.Value;
        }

        public async Task<string> GetMessagesAsync(string threadId)
        {
            var response = await client.GetMessagesAsync(threadId);
            var messages = response.Value.Data;
            var latestMessage = messages.FirstOrDefault(m => m.Role == MessageRole.Assistant);
            if (latestMessage != null)
            {
                var message = string.Join(Environment.NewLine, latestMessage.ContentItems
                    .OfType<MessageTextContent>()
                    .Select(c => c.Text));

                return message;
            }
            return null;
        }


        public async Task<Sprite> DownloadImage(string prompt)
        {
            imageUrl = null;
            string cacheImagePath = CachedImagesManager.Instance.GetImagePath(prompt);
            if (cacheImagePath == null)
            {
                await GetImageFromOpenAIV2(prompt, result => imageUrl = result);
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    try
                    {
                        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl))
                        {
                            var asyncOperation = www.SendWebRequest();
                            while (!asyncOperation.isDone)
                            {
                                await System.Threading.Tasks.Task.Yield(); // Allows the method to asynchronously wait
                            }
                            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                            {
                                Debug.LogError(www.error);
                            }
                            else
                            {
                                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                                CachedImage(prompt,texture);
                                Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                                return newSprite;
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Debug.Log(ex.Message);
                    }
                }
                else
                {
                    Debug.LogError("Failed to get image URL.");
                }
            }
            else
            {
                //get image from local cache
                byte[] cacheImageBytes = await File.ReadAllBytesAsync(cacheImagePath);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(cacheImageBytes);
                Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                return newSprite;
            }

            return null;
        }

        private void CachedImage(string prompt,Texture2D texture)
        {
            byte[] textureBytes = texture.EncodeToPNG();

            var fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            string imageStorePath = Application.dataPath + "/images/" + fileName;
            File.WriteAllBytes(imageStorePath, textureBytes);

            CachedImagesManager.Instance.SavePrompt(prompt, imageStorePath);
        }

        public async System.Threading.Tasks.Task GetImageFromOpenAIV2(string prompt, System.Action<string> callback)
        {
            //var prompt = "A basketball game on Jupiter";
            var openAIOptions = new ImageGenerationOptions();
            var openAIClientOptions = new OpenAIClientOption();
            var cancellationToken = new CancellationToken();

            openAIOptions.Prompt = prompt;
            openAIOptions.ImageCount = 1;
            openAIOptions.Style = ImageGenerationStyle.Vivid;
            openAIOptions.Size = ImageSize.Size1024x1024;
            openAIOptions.Quality = ImageGenerationQuality.Hd;
            openAIOptions.DeploymentName = ConfigManager.config.OpenAI.DeploymentName;
            openAIClientOptions.Endpoint = ConfigManager.config.OpenAIClient.Endpoint;
            openAIClientOptions.ApiKey = ConfigManager.config.OpenAIClient.ApiKey;

            // Initialize OpenAI client
            var client = new OpenAIClient(new Uri(openAIClientOptions.Endpoint), new Azure.AzureKeyCredential(openAIClientOptions.ApiKey));

            // Call OpenAI API to generate images
            var response = await client.GetImageGenerationsAsync(openAIOptions, cancellationToken);
            var generations = response.Value.Data;
            string imageUrl = generations[0].Url.ToString();
            callback?.Invoke(imageUrl);
        }

    }
    public class SubmitToolOutput
    {
        public string RunId { get; set; }
        public string Outputs { get; set; }
    }

}