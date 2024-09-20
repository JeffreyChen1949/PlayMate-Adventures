using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;

namespace Assets.Models
{
    public class PromptData
    {
        public string Prompt { get; set; }
        public string ImagePath { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class CachedImagesManager
    {
        private static CachedImagesManager cachedImagesManager;
        public static CachedImagesManager Instance => cachedImagesManager ??= new CachedImagesManager(Application.dataPath + "/images/" + "cachedImages.json");
        private string filePath;

        private CachedImagesManager(string filePath)
        {
            this.filePath = filePath;
            EnsureFileExists();
        }

        private void EnsureFileExists()
        {
            if (!File.Exists(filePath))
            {
                var emptyData = new { prompts = new List<PromptData>() };
                File.WriteAllText(filePath, JsonSerializer.Serialize(emptyData));
            }
        }

        public List<PromptData> LoadPrompts()
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<PromptData>>>(json);
            return data["prompts"];
        }

        public void SavePrompt(string prompt, string imagePath)
        {
            var prompts = LoadPrompts();
            var newPrompt = new PromptData
            {
                Prompt = prompt,
                ImagePath = imagePath,
                CreatedAt = DateTime.Now
            };

            prompts.Add(newPrompt);

            var updatedData = new { prompts = prompts };
            File.WriteAllText(filePath, JsonSerializer.Serialize(updatedData));
        }

        public string GetImagePath(string prompt)
        {
            var prompts = LoadPrompts();
            var promptData = prompts.Find(p => p.Prompt == prompt);
            return promptData?.ImagePath;
        }
    }

    class TestCachedImagesManager
    {
        static void Test()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(appDataPath, "YourAppName");
            Directory.CreateDirectory(folderPath); // 确保文件夹存在

            string filePath = Path.Combine(folderPath, "prompts.json");

            

            // 保存新的 prompt 和图片路径
            CachedImagesManager.Instance.SavePrompt("Generate a mountain landscape", "images/mountain_landscape.png");

            // 查询 prompt 对应的图片路径
            string imagePath = CachedImagesManager.Instance.GetImagePath("Generate a mountain landscape");
            Console.WriteLine($"Image Path: {imagePath}");
        }
    }
}
