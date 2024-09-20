using Azure.AI.Vision.Face;
using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Models
{
    public class HeadMovementDetector
    {
        private readonly FaceClient _faceClient;
        private readonly List<double> _pitchAngles = new List<double>();

        // 定义一个阈值来判断何时认定为点头或摇头
        private const double PitchThreshold = 7; // Pitch的变化阈值
        private const double YawThreshold = 7; // Yaw的变化阈值
        private const int FrameBuffer = 2; // 检测帧的数量

        public HeadMovementDetector()
        {
            //replace the url and credential with your own
            string endpoint = "https://****.cognitiveservices.azure.com/";
            Uri Endpoint = new Uri(endpoint);
            AzureKeyCredential credential = new AzureKeyCredential("*****");
            _faceClient = new FaceClient(Endpoint, credential);
        }

        public async Task<string> AnalyzeHeadMovementAsync(List<byte[]> imageBytesList)
        {
            List<HeadPose> _headPoses = new List<HeadPose>();
            List<double> _pitchAngles = new List<double>();
            List<double> _yawAngles = new List<double>();
            foreach (var imageBytes in imageBytesList)
            {   // 将 byte[] 转换为 BinaryData
                BinaryData binaryData = new BinaryData(imageBytes);
                // 记录开始时间
                //Debug.Log("face api call start time:");
                var detectResponse = await _faceClient.DetectAsync(
                    binaryData,
                    FaceDetectionModel.Detection03,
                    FaceRecognitionModel.Recognition04,
                    returnFaceId: false,
                    returnFaceAttributes: new[] { FaceAttributeType.Detection03.HeadPose },
                    returnFaceLandmarks: true,
                    returnRecognitionModel: true,
                    faceIdTimeToLive: 120
                    );

                //Debug.Log("face api call stop time:" + Time.time.ToString());
                var detectedFaces = detectResponse.Value;
                if (detectedFaces.Count > 0)
                {
                    var headPose = detectedFaces[0].FaceAttributes.HeadPose;
                    _headPoses.Add(headPose);
                    _pitchAngles.Add(headPose.Pitch);
                    _yawAngles.Add(headPose.Yaw);

                    // 只保留最近的 FrameBuffer 帧数据
                    if (_pitchAngles.Count > FrameBuffer) _pitchAngles.RemoveAt(0);
                    if (_yawAngles.Count > FrameBuffer) _yawAngles.RemoveAt(0);
                }
            }
            if (_headPoses.Count >= FrameBuffer)
            {

                if (IsNodding(_pitchAngles))
                {
                    Debug.Log("Detected a nodding motion (点头).");
                    return "yes";
                }
                else if (IsShaking(_yawAngles))
                {
                    Debug.Log("Detected a shaking motion (摇头).");
                    return "no";
                }
                else
                {
                    //Debug.Log("No significant head movement detected.");
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        private bool IsNodding(List<double> _pitchAngles)
        {
            // 计算最近几帧的 Pitch 角度变化量
            double pitchChange = _pitchAngles[_pitchAngles.Count - 1] - _pitchAngles[0];

            // 如果 Pitch 变化超过阈值，则认为是点头
            return Math.Abs(pitchChange) > PitchThreshold;
        }

        private bool IsShaking(List<double> _yawAngles)
        {
            // 计算最近几帧的 Yaw 角度变化量
            double yawChange = _yawAngles[_yawAngles.Count - 1] - _yawAngles[0];

            // 如果 Yaw 变化超过阈值，则认为是摇头
            return Math.Abs(yawChange) > YawThreshold;
        }
    }
}
