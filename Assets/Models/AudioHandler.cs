using Microsoft.CognitiveServices.Speech;
using System.Threading.Tasks;

namespace Assets.Models
{
    public class AudioHandler
    {
        //replace with your personal speech key and region
        string speechKey = "***************";
        string speechRegion = "***********";
        SpeechConfig speechConfig;
        SpeechSynthesizer speechSynthesizer;
        public AudioHandler()
        {
            speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechSynthesisVoiceName = "en-US-AnaNeural";
            speechSynthesizer = new SpeechSynthesizer(speechConfig);
        }
        public async Task Speak(string text)
        {
            if (speechSynthesizer != null)
            {
                await speechSynthesizer.StopSpeakingAsync();
            }
            bool isSpeakFinished = false;
            _ = Task.Run(() =>
            {
                while (!isSpeakFinished)
                {
                    LipSyncController.UpdateLipSync("Ah");
                    LipSyncController.UpdateLipSync("Ah");
                    LipSyncController.UpdateLipSync("Ah");
                }
            });
            var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(text);
            isSpeakFinished = true;
            OutputSpeechSynthesisResult(speechSynthesisResult, text);
        }
        public void OutputSpeechSynthesisResult(SpeechSynthesisResult speechSynthesisResult, string text)
        {
            switch (speechSynthesisResult.Reason)
            {
                case ResultReason.SynthesizingAudioCompleted:
                    //Debug.Log($"Speech synthesized for text: [{text}]");
                    break;
                case ResultReason.Canceled:
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                    //Debug.Log($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        //Debug.Log($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        //Debug.Log($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                        //Debug.Log($"CANCELED: Did you set the speech resource key and region values?");
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
