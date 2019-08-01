using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoIt;
using Google.Cloud.Speech.V1;

namespace Active_Window_Send_Speech_To_Text_Console
{
    // https://cloud.google.com/speech-to-text/docs/streaming-recognize
    class Program
    {
        static void Main(string[] args)
        {
            const int sixteenseconds = 60;
            int length_min = 1;

            var task = StreamingMicRecognizeAsync(length_min * sixteenseconds);
            task.Wait();
        }

        static async Task<object> StreamingMicRecognizeAsync(int seconds)
        {
            var speech = SpeechClient.Create();
            var streamingCall = speech.StreamingRecognize();
            // Write the initial request with the config.
            await streamingCall.WriteAsync(
                new StreamingRecognizeRequest()
                {
                    StreamingConfig = new StreamingRecognitionConfig()
                    {
                        Config = new RecognitionConfig()
                        {
                            Encoding =
                            RecognitionConfig.Types.AudioEncoding.Linear16,
                            SampleRateHertz = 16000,
                            LanguageCode = "tr",
                        },
                        InterimResults = true,
                    }
                });
            // Print responses as they arrive.
            Task printResponses = Task.Run(async () =>
            {
                //AutoItX.Run("notepad.exe", null);
                int test = AutoItX.WinWaitActive("Untitled - Notepad");
                while (await streamingCall.ResponseStream.MoveNext(
                    default(CancellationToken)))
                {
                    double dogruluk_orani = streamingCall.ResponseStream.Current.Results[0].Alternatives[0].Confidence;

                    if (dogruluk_orani > 0.60)
                    {
                        Console.WriteLine("D/O: " + dogruluk_orani+ " | " + streamingCall.ResponseStream.Current.Results[0].Alternatives[0].Transcript);
                        AutoItX.Send(streamingCall.ResponseStream.Current.Results[0].Alternatives[0].Transcript + "\n");
                    }
                    else
                    {
                        Console.WriteLine("Anlaşılamadı...");
                    }

                    foreach (StreamingRecognitionResult result in streamingCall.ResponseStream.Current.Results)
                    {
                        foreach (SpeechRecognitionAlternative alternative in result.Alternatives)
                        {
                            Console.WriteLine(alternative.Transcript);
                        }
                    }
                }
            });
            // Read from the microphone and stream to API.
            object writeLock = new object();
            bool writeMore = true;
            var waveIn = new NAudio.Wave.WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new NAudio.Wave.WaveFormat(16000, 1);
            waveIn.DataAvailable +=
                (object sender, NAudio.Wave.WaveInEventArgs args) =>
                {
                    lock (writeLock)
                    {
                        if (!writeMore) return;
                        streamingCall.WriteAsync(
                            new StreamingRecognizeRequest()
                            {
                                AudioContent = Google.Protobuf.ByteString
                                    .CopyFrom(args.Buffer, 0, args.BytesRecorded)
                            }).Wait();
                    }
                };
            waveIn.StartRecording();
            Console.WriteLine("Speak now.");
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            // Stop recording and shut down.
            waveIn.StopRecording();
            lock (writeLock) writeMore = false;
            await streamingCall.WriteCompleteAsync();
            await printResponses;
            return 0;
        }
    }
}
