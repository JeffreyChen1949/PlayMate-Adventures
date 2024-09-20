using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using static System.Environment;
using System.Threading;
using Azure.AI.OpenAI;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech;
using System.IO;
using Azure.AI.OpenAI.Assistants;
using Assets.Models;
using PimDeWitte.UnityMainThreadDispatcher;
using Azure;
using System.Linq;
using System.Text.Json;



public class HelloButton : MonoBehaviour
{
    private Animator animator;
    public Button HButton;
    public Button DButton;
    public Image imageComponent;
    public Button loadImageButton;
    public Button StartCameraButton;
    public Button CaptureImageButton;
    public RawImage rawImage; // 用于显示摄像头视频的UI组件
    private WebCamTexture webCamTexture;
    public float captureInterval = 2.0f; // 间隔时间（秒）
    public float displayDuration = 15.0f; // 显示时间（秒）

    //audio
    public Text outputText;
    public Text inputText;
    public Button recoButton;
    SpeechRecognizer recognizer;
    SpeechConfig config;
    AudioConfig audioInput;
    PushAudioInputStream pushStream;
    private object threadLocker = new object();
    private bool recognitionStarted = false;
    private string inputmessage;
    private string outputmessage;
    AudioSource audioSource;
    int lastSample = 0;
    private bool micPermissionGranted = false;

    private Coroutine hideCoroutine;
    private bool isAnimating = false; // 判断动画是否正在播放

    //capture images
    private HeadMovementDetector detector;
    private Coroutine captureCoroutine;
    private SynchronizationContext unityContext;
    private List<CancellationTokenSource> GestureMoitorCtss = new List<CancellationTokenSource>();
    private object lockerForctsToStopGestureMoitoring = new object();


    private object queueLock = new object(); 
    private object queueLockforStop = new object();
    bool isCameraOpen = false;

    //Assistant Client related
    AzureAssistantClient assistantClient;
    AssistantThread assistantThread;
    //replace below with your personal assistant ID
    string assistantId = "***************";

    AudioHandler audioHandler;


    List<string> actions = new List<string> { "clap", "jump", "head_nod", "dance" };
    ThreadRun run = null;
    // Start is called before the first frame update
    private async void Start()
    {
        ConfigManager.LoadConfig();
        detector = new HeadMovementDetector();
        animator = GetComponent<Animator>();

        HButton.onClick.AddListener(OnHelloButtonClick);
        DButton.onClick.AddListener(OnDanceButtonClick);
        StartCameraButton.onClick.AddListener(OnStartCameraClick);
        CaptureImageButton.onClick.AddListener(CaptureAndSendImage);
        //loadImageButton.onClick.AddListener(async () => await testDownloadImage("house"));

        // 初始时隐藏RawImage和ImageComponent
        rawImage.gameObject.SetActive(false);
        imageComponent.gameObject.SetActive(false);

        unityContext = SynchronizationContext.Current;

        if (outputText == null)
        {
            //UnityEngine.Debug.LogError("outputText property is null! Assign a UI Text element to it.");
        }
        else if (recoButton == null)
        {
            //inputmessage = "recoButton property is null! Assign a UI Button to it.";
            //UnityEngine.Debug.LogError(inputmessage);
        }
        else
        {
            // Continue with normal initialization, Text and Button objects are present.
#if PLATFORM_ANDROID
                                                        // Request to use the microphone, cf.
                                                        // https://docs.unity3d.com/Manual/android-RequestingPermissions.html
                                                        message = "Waiting for mic permission";
                                                        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                                                        {
                                                            Permission.RequestUserPermission(Permission.Microphone);
                                                        }
#elif PLATFORM_IOS
                                                        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                                                        {
                                                            Application.RequestUserAuthorization(UserAuthorization.Microphone);
                                                        }
#else
            micPermissionGranted = true;
            //inputmessage = "Click button to recognize speech";
#endif
            //speech related initialization, replace it with your personal subscription and region
            config = SpeechConfig.FromSubscription("************", "*****");
            pushStream = AudioInputStream.CreatePushStream();
            audioInput = AudioConfig.FromStreamInput(pushStream);
            recognizer = new SpeechRecognizer(config, audioInput);
            recognizer.Recognizing += RecognizingHandler;
            recognizer.Recognized += RecognizedHandler;
            recognizer.Canceled += CanceledHandler;
            recoButton.onClick.AddListener(RecoButtonClick);
            foreach (var device in Microphone.devices)
            {
                Debug.Log("DeviceName: " + device);
            }
            audioSource = GameObject.Find("MyAudioSource").GetComponent<AudioSource>();
        }

        //replace with your own assistant endpoint and key
        string endpoint = "https://****.openai.azure.com/";
        string key = "******";
        assistantClient = new AzureAssistantClient(endpoint, key);
        assistantThread = await assistantClient.CreateThreadAsync();
        audioHandler = new AudioHandler();
        OnStartCameraClick();
        RecoButtonClick();
        await ActionForInput("Hello");
    }

    private byte[] ConvertAudioClipDataToInt16ByteArray(float[] data)
    {
        MemoryStream dataStream = new MemoryStream();
        int x = sizeof(Int16);
        Int16 maxValue = Int16.MaxValue;
        int i = 0;
        while (i < data.Length)
        {
            dataStream.Write(BitConverter.GetBytes(Convert.ToInt16(data[i] * maxValue)), 0, x);
            ++i;
        }
        byte[] bytes = dataStream.ToArray();
        dataStream.Dispose();
        return bytes;
    }

    private void RecognizingHandler(object sender, SpeechRecognitionEventArgs e)
    {
        lock (threadLocker)
        {
            inputmessage = e.Result.Text;
            //Debug.Log("RecognizingHandler: " + inputmessage);
        }
    }

    private async void RecognizedHandler(object sender, SpeechRecognitionEventArgs e)
    {
        inputmessage = string.Empty;
        lock (threadLocker)
        {
            inputmessage = e.Result.Text;
        }
        if (inputmessage != "")
        {
            //Debug.Log($"trigger recognizedhandler message is:{inputmessage}");
            await ActionForInput(inputmessage);
        }
    }

    public async Task ActionForInput(string inputVoiceMessage)
    {
        var gestureMoitorCts = new CancellationTokenSource();
        //Debug.Log($"enter ActionForInput just now, the token is: {gestureMoitorCts.IsCancellationRequested}");
        lock (lockerForctsToStopGestureMoitoring)
        {
            for (int i = GestureMoitorCtss.Count - 1; i >= 0; i--)
            {
                GestureMoitorCtss[i]?.Cancel();
                GestureMoitorCtss.RemoveAt(i);
            }
            GestureMoitorCtss.Add(gestureMoitorCts);
            //Debug.Log($"after remove list, the token is: {gestureMoitorCts.IsCancellationRequested}");
        }

        var assistantResponse = await SendMessageToAssistant(inputVoiceMessage);
        if (!string.IsNullOrEmpty(assistantResponse))
        {
            outputmessage = assistantResponse;
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(async () =>
            {
                animator.SetBool("talk", true);
                await Task.Delay(10);
            });
            await audioHandler.Speak(assistantResponse);
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(async () =>
            {
                animator.SetBool("talk", false);
                await Task.Delay(10);
            });

            var response = await OnSpeechEnd(gestureMoitorCts.Token);
            if (!string.IsNullOrEmpty(response))
            {
                inputmessage = response;
                await ActionForInput(response);
            }
            gestureMoitorCts?.Cancel();
            //Debug.Log($"after completedMonitor == checkHeadGestureTask cancel{gestureMoitorCts.ToString()}");
        }
    }

    public async Task<string> RunAndFetchResponseAsync(string threadId, string assistantId, string messageText)
    {
        await assistantClient.AddMessageToThreadAsync(threadId, messageText);
        // Run the thread
        run = await assistantClient.RunThreadAsync(threadId, assistantId);

        string toolCallOutput = "";
        string responseMessage = "";
        // Wait for the assistant to respond
        while (true)
        {
            toolCallOutput = "";
            responseMessage = "";
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            var runResponse = await assistantClient.client.GetRunAsync(threadId, run.Id);

            if (runResponse.Value.Status == RunStatus.RequiresAction)
            {
                var requiredAction = runResponse.Value.RequiredAction;

                if (requiredAction != null)
                {
                    //RequiredFunctionToolCall toolCall = ((RequiredFunctionToolCall)((SubmitToolOutputsAction)runResponse.Value.RequiredAction).ToolCalls[0]);
                    // 检查 RequiredAction 是否是 SubmitToolOutputsAction 类型
                    if (runResponse.Value.RequiredAction is SubmitToolOutputsAction submitAction)
                    {
                        // 确保 ToolCalls 列表不为空
                        if (submitAction.ToolCalls != null && submitAction.ToolCalls.Count > 0)
                        {
                            // 获取 ToolCall 并检查其是否是 RequiredFunctionToolCall 类型
                            if (submitAction.ToolCalls[0] is RequiredFunctionToolCall toolCall)
                            {
                                // 现在可以直接访问 toolCall 的属性
                                Console.WriteLine($"Function Name: {toolCall.Name}");
                                Console.WriteLine($"Arguments: {toolCall.Arguments}");
                                string functionName = toolCall.Name;
                                string functionArguments = toolCall.Arguments;
                                using JsonDocument doc = JsonDocument.Parse(functionArguments);
                                //string description = doc.RootElement.GetProperty("description").GetString();
                                //action_name
                                if (functionName != null)
                                {
                                    if (functionName == "create_image")
                                    {
                                        string description = doc.RootElement.GetProperty("description").GetString();
                                        //await testDownloadImage(description);
                                        try
                                        {
                                            await UnityMainThreadDispatcher.Instance().EnqueueAsync(async () =>
                                            {
                                                //imageComponent.gameObject.SetActive(false);
                                                var newSprite = await assistantClient.DownloadImage(description);
                                                if (newSprite != null)
                                                {
                                                    imageComponent.sprite = newSprite;
                                                    imageComponent.gameObject.SetActive(true);
                                                }
                                            });
                                            toolCallOutput = "success:true";

                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.Log(ex.Message);
                                        }
                                    }
                                    if (functionName == "character_animation")
                                    {
                                        string actionName = doc.RootElement.GetProperty("animation_name").GetString();
                                        try
                                        {
                                            await UnityMainThreadDispatcher.Instance().EnqueueAsync(async () =>
                                            {
                                                //imageComponent.gameObject.SetActive(false);
                                                if (!string.IsNullOrEmpty(actionName) && actionName.Contains(actionName))
                                                {
                                                    switch (actionName)
                                                    {
                                                        case "clap":

                                                            animator.SetBool("talk", false);
                                                            animator.SetBool("Thumb", true);
                                                            break;
                                                        case "jump":
                                                            animator.SetBool("talk", false);
                                                            animator.SetBool("Jump", true);
                                                            break;
                                                        case "dance":
                                                            animator.SetBool("talk", false);
                                                            animator.SetBool("Dance", true);
                                                            break;
                                                        case "head_nod":
                                                            animator.SetBool("talk", false);
                                                            animator.SetBool("HeadNode", true);
                                                            break;
                                                    }
                                                }
                                                await Task.Delay(6000);
                                            });
                                            toolCallOutput = "success:true";
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.Log(ex.Message);
                                        }
                                    }
                                    if (functionName == "switch_game_scene")
                                    {
                                        string gameName = doc.RootElement.GetProperty("game_name").GetString();
                                        try
                                        {
                                            await UnityMainThreadDispatcher.Instance().EnqueueAsync(async () =>
                                            {
                                                imageComponent.gameObject.SetActive(false);
                                                switch (gameName)
                                                {
                                                    case "Fruit Explorer":
                                                        BackgroundManager.SwitchBackground("vikingRoom");
                                                        break;
                                                    case "Shopping Adventure":
                                                        BackgroundManager.SwitchBackground("mouseHome");
                                                        break;
                                                    default:
                                                        BackgroundManager.SwitchBackground("kidsRoom");
                                                        break;
                                                };
                                                await Task.Delay(10);
                                            });
                                            toolCallOutput = "success:true";
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.Log(ex.Message);
                                        }
                                    }
                                }

                                var toolOutput = new ToolOutput
                                {
                                    ToolCallId = toolCall.Id,
                                    Output = toolCallOutput
                                };

                                var toolOutputs = new List<ToolOutput> { toolOutput };

                                await assistantClient.client.SubmitToolOutputsToRunAsync(run, toolOutputs);

                                await UnityMainThreadDispatcher.Instance().EnqueueAsync(async () =>
                                {
                                    await Task.Delay(10);
                                    animator.SetBool("Thumb", false);
                                    animator.SetBool("Jump", false);
                                    animator.SetBool("Dance", false);
                                    animator.SetBool("HeadNode", false);

                                });

                                runResponse = await assistantClient.client.GetRunAsync(threadId, run.Id);

                                if (runResponse != null && runResponse.Value.Status == RunStatus.Completed)
                                {
                                    responseMessage = await assistantClient.GetMessagesAsync(threadId);
                                    break;
                                }
                            }
                        }
                    }
                }

            }

            if (runResponse.Value.Status == RunStatus.Completed)
            {
                responseMessage = await assistantClient.GetMessagesAsync(threadId);
                break;
            }
        }

        return responseMessage;
    }
    private void CanceledHandler(object sender, SpeechRecognitionCanceledEventArgs e)
    {
        lock (threadLocker)
        {
            inputmessage = e.ErrorDetails.ToString();
            Debug.Log("CanceledHandler: " + inputmessage);
        }
    }
    void Disable()
    {
        recognizer.Recognizing -= RecognizingHandler;
        recognizer.Recognized -= RecognizedHandler;
        recognizer.Canceled -= CanceledHandler;
        pushStream.Close();
        recognizer.Dispose();
    }
    void FixedUpdate()
    {
#if PLATFORM_ANDROID
                if (!micPermissionGranted && Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    micPermissionGranted = true;
                    message = "Click button to recognize speech";
                }
#elif PLATFORM_IOS
                if (!micPermissionGranted && Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    micPermissionGranted = true;
                    message = "Click button to recognize speech";
                }
#endif
        lock (threadLocker)
        {
            if (recoButton != null)
            {
                recoButton.interactable = micPermissionGranted;
            }
            if (inputText != null)
            {
                inputText.text = inputmessage;
            }
            if (outputText != null)
            {
                outputText.text = outputmessage;
            }
        }

        if (Microphone.IsRecording(Microphone.devices[0]) && recognitionStarted == true)
        {
            GameObject.Find("AudioController").GetComponentInChildren<Text>().text = "Stop";
            int pos = Microphone.GetPosition(Microphone.devices[0]);
            int diff = pos - lastSample;

            if (diff > 0)
            {
                float[] samples = new float[diff * audioSource.clip.channels];
                audioSource.clip.GetData(samples, lastSample);
                byte[] ba = ConvertAudioClipDataToInt16ByteArray(samples);
                if (ba.Length != 0)
                {
                    //Debug.Log("pushStream.Write pos:" + Microphone.GetPosition(Microphone.devices[0]).ToString() + " length: " + ba.Length.ToString());
                    pushStream.Write(ba);
                }
            }
            lastSample = pos;
        }
        else if (!Microphone.IsRecording(Microphone.devices[0]) && recognitionStarted == false)
        {
            GameObject.Find("AudioController").GetComponentInChildren<Text>().text = "Start";
        }
    }
    public async void RecoButtonClick()
    {
        if (recognitionStarted)
        {
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(true);

            if (Microphone.IsRecording(Microphone.devices[0]))
            {
                Debug.Log("Microphone.End: " + Microphone.devices[0]);
                Microphone.End(null);
                lastSample = 0;
            }

            lock (threadLocker)
            {
                recognitionStarted = false;
                Debug.Log("RecognitionStarted: " + recognitionStarted.ToString());
            }
        }
        else
        {
            if (!Microphone.IsRecording(Microphone.devices[0]))
            {
                Debug.Log("Microphone.Start: " + Microphone.devices[0]);
                audioSource.clip = Microphone.Start(Microphone.devices[0], true, 200, 16000);
                Debug.Log("audioSource.clip channels: " + audioSource.clip.channels);
                Debug.Log("audioSource.clip frequency: " + audioSource.clip.frequency);
            }

            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
            lock (threadLocker)
            {
                recognitionStarted = true;
                Debug.Log("RecognitionStarted: " + recognitionStarted.ToString());
            }
        }
    }
    void OnHelloButtonClick()
    {
        if (animator.GetBool("B_Hello") == true)
        {
            animator.SetBool("B_Hello", false);
            //StopAnimation();
        }
        else
        {
            animator.SetBool("B_Hello", true);
            //StartAnimation();
        }
    }
    void OnDanceButtonClick()
    {
        if (animator.GetBool("B_Dance") == true)
        {
            animator.SetBool("B_Dance", false);
            //StopAnimation();
        }
        else
        {
            animator.SetBool("B_Dance", true);
            //StartAnimation();
        }
    }
    private void OnStartCameraClick()
    {
        if (!isCameraOpen)
        {
            rawImage.gameObject.SetActive(true);
            StartCamera();
            isCameraOpen = true;
        }
        else
        {
            rawImage.gameObject.SetActive(false);
            OnDestroy();
            isCameraOpen = false;
        }

    }
    private IEnumerator HideImageAfterDelay(MaskableGraphic image)
    {
        yield return new WaitForSeconds(displayDuration);
        image.gameObject.SetActive(false);
    }
    private async Task<Texture2D> DownloadImageAsync(string url)
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                byte[] imageData = await httpClient.GetByteArrayAsync(url);

                // 创建纹理并加载图片数据
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(imageData))
                {
                    return texture;
                }
                else
                {
                    Debug.LogError("Failed to load image data into Texture2D.");
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error downloading image: {ex.Message}");
            return null;
        }
    }
    public void StartCamera()
    {
        // 检查是否有摄像头设备
        if (WebCamTexture.devices.Length > 0)
        {
            // 获取第一个摄像头设备
            WebCamDevice camera = WebCamTexture.devices[0];
            // 创建WebCamTexture对象并绑定摄像头设备
            webCamTexture = new WebCamTexture(camera.name);
            // 开始播放摄像头视频
            webCamTexture.Play();
            // 将摄像头视频设置为RawImage组件的纹理
            rawImage.texture = webCamTexture;
        }
        else
        {
            Debug.LogWarning("没有找到摄像头设备");
        }
    }
    void OnDestroy()
    {
        // 停止摄像头
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }
    }
    private IEnumerator CaptureImageRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(captureInterval);
            CaptureAndSendImage();
        }
    }
    private void CaptureAndSendImage()
    {
        // 创建一个新的Texture2D，用于保存摄像头当前帧的图像
        Texture2D capturedTexture = new Texture2D(webCamTexture.width, webCamTexture.height);
        capturedTexture.SetPixels(webCamTexture.GetPixels());
        capturedTexture.Apply();

        Sprite newSprite = Sprite.Create(capturedTexture, new Rect(0, 0, capturedTexture.width, capturedTexture.height), new Vector2(0.5f, 0.5f));
        imageComponent.sprite = newSprite;
        imageComponent.gameObject.SetActive(true);
        hideCoroutine = StartCoroutine(HideImageAfterDelay(imageComponent));
    }

    int runCycle = 0;
    public async Task<string> OnSpeechEnd(CancellationToken token)
    {
        List<Task<string>> imageProcessTasks = new List<Task<string>>();
        Queue<byte[]> capturedImagesQueue = new Queue<byte[]>();

        if (!token.IsCancellationRequested)
        {
            string result = string.Empty;
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(async () =>
            {
                if (captureCoroutine != null)
                {
                    StopCoroutine(captureCoroutine);
                    Debug.Log("stop root coroutine for the new one to start!");
                }
                try
                {
                    captureCoroutine = StartCoroutine(CaptureFramesForDuration(70f, token, capturedImagesQueue));


                    for (int i = 0; i < 10; i++)
                    {
                        imageProcessTasks.Add(Task.Run(async () => await ProcessCapturedImagesAsync(token, capturedImagesQueue)));
                    }
                    var completeTask = Task.WhenAny(imageProcessTasks);
                    var tResult = await completeTask;
                    //Debug.LogFormat("head checking result:{0}", tResult.Result);
                    result = tResult.Result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to start coroutine: {ex.Message}");
                }

            });
            return result;
        }
        return string.Empty;
    }
    private IEnumerator CaptureFramesForDuration(float duration, CancellationToken token, Queue<byte[]> capturedImagesQueue)
    {
        lock (queueLock)
        {
            capturedImagesQueue.Clear();
        }
        float endTime = Time.time + duration;

        while (Time.time < endTime && !token.IsCancellationRequested)
        {
            //Debug.Log($"Time.time: {Time.time}, EndTime: {endTime}");
            //Debug.Log($"token: {token.IsCancellationRequested}");
            Texture2D capturedTexture = new Texture2D(webCamTexture.width, webCamTexture.height);
            capturedTexture.SetPixels(webCamTexture.GetPixels());
            capturedTexture.Apply();

            byte[] imageBytes = capturedTexture.EncodeToPNG();
            lock (queueLock)
            {
                capturedImagesQueue.Enqueue(imageBytes);
            }

            yield return new WaitForSeconds(0.2f);
        }
        //Debug.Log($"CaptureFramesForDuration stopped and final token is:  {token.IsCancellationRequested}");
        Debug.Log("1 minute of capture complete.");
    }

    private async Task<string> ProcessCapturedImagesAsync(CancellationToken token, Queue<byte[]> capturedImagesQueue)
    {
        List<byte[]> imageBytes = new List<byte[]>();
        int i = 0;
        string analyzeResult = string.Empty;
        //bool shouldContinueProcessing = false;
        var startTime = DateTime.Now;
        while (!token.IsCancellationRequested)
        {
            //Debug.LogFormat("while start {0}", runCycle.ToString());
            if (DateTime.Now - startTime > TimeSpan.FromSeconds(70))
            {
                Debug.LogFormat("ProcessCapturedImagesAsync is closed due to time end!{0}", runCycle.ToString());
                break;
            }

            if (capturedImagesQueue.Count > 0)
            {
                imageBytes.Clear();
                lock (queueLock)
                {
                    if (capturedImagesQueue.Count >= 2)
                    {
                        //Debug.LogFormat("prepare 2 images!{0}", runCycle.ToString());
                        imageBytes.Add(capturedImagesQueue.Dequeue());
                        imageBytes.Add(capturedImagesQueue.Dequeue());
                    }
                }
                // 确保有足够的图片进行比较
                if (imageBytes.Count < 2)
                {
                    continue;
                }
                i++;
                //Debug.LogWarning("start to analyze images:" + i.ToString());
                analyzeResult = await detector.AnalyzeHeadMovementAsync(imageBytes);

                // 根据分析结果决定是否停止截图和处理
                if (analyzeResult == "yes" || analyzeResult == "no")
                {
                    //Debug.Log("find gesture!");
                    break;
                }

                //Debug.Log("didn't find gesture, go to complete!" + i.ToString());
            }
            await Task.Delay(1000); // 稍作延时，避免过度占用资源
        }
        Debug.Log($"Image processing complete!!!!!!!,current token is {token.IsCancellationRequested}");
        return analyzeResult;
    }

    void StartAnimation()
    {
        if (!isAnimating)
        {
            animator.SetTrigger("StartAnimationTrigger"); // 触发动画
            isAnimating = true;
        }
    }

    void StopAnimation()
    {
        if (isAnimating)
        {
            animator.ResetTrigger("StartAnimationTrigger"); // 重置触发器
            animator.SetTrigger("StopAnimationTrigger"); // 触发停止动画的状态
            isAnimating = false;
        }
    }

    public async Task<string> SendMessageToAssistant(string message)
    {
        string threadId = assistantThread.Id;
        string userMessage = message;
        string response = null;
        if (run != null)
        {
            var runResponse = await assistantClient.client.GetRunAsync(threadId, run.Id);
            if (runResponse.Value.Status == RunStatus.Completed)
            {
                response = await RunAndFetchResponseAsync(threadId, assistantId, userMessage);
            }
            else
            {
                outputmessage = "i'm busy now, pleae wait a moment!";
                await audioHandler.Speak(outputmessage);
                Debug.Log("wait run to complete, it's calling function or replying!");
            }
        }
        else
        {
            response = await RunAndFetchResponseAsync(threadId, assistantId, userMessage);
        }
        //这里的response可能出现3中情况：文字消息，图片，action，每说一句话可能触发3中可能情况
        return response;
    }

}






