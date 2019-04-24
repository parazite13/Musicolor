using System;
using Q42.HueApi;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;
using System.Linq;
using Q42.HueApi.Models.Bridge;
using System.IO;
using System.Threading;

namespace Musicolor
{
    public class Hue
    {
        public static StreamingHueClient Client;
        public static StreamingGroup StreamingGroup;
        public static bool Ready = false;
        public static EntertainmentLayer BaseLayer;

        private const string credentialPath = "hue_credentials.json";
        private static HueCredential hueCredential;

        private static CancellationTokenSource cancellationTokenSource;

        public static async void Setup(string ip)
        {
            RegisterEntertainmentResult registeredInfos;

            // Is the Hue credentials present ?
            if (!File.Exists(credentialPath))
            {
                Console.WriteLine("No credentials found please press the bridge button");
                registeredInfos = await LocalHueClient.RegisterAsync(ip, "Musicolor", "raspberry", true);
                hueCredential = new HueCredential()
                {
                    Username = registeredInfos.Username,
                    Key = registeredInfos.StreamingClientKey
                };
                File.WriteAllText(credentialPath, Newtonsoft.Json.JsonConvert.SerializeObject(hueCredential));
                Console.WriteLine("Registration success credentials are :");
                Console.WriteLine("Username : " + registeredInfos.Username);
                Console.WriteLine("Key : " + registeredInfos.StreamingClientKey);
            }
            else
            {
                hueCredential = Newtonsoft.Json.JsonConvert.DeserializeObject<HueCredential>(File.ReadAllText(credentialPath));
            }

            registeredInfos = new RegisterEntertainmentResult()
            {
                Username = hueCredential.Username,
                StreamingClientKey = hueCredential.Key
            };
            
            Console.WriteLine("Get client");
            Client = new StreamingHueClient(ip, registeredInfos.Username, registeredInfos.StreamingClientKey);

            //Get the entertainment group
            Console.WriteLine("Get entertainment group");
            var all = await Client.LocalHueClient.GetEntertainmentGroups();
            var group = all.FirstOrDefault();

            //Create a streaming group
            Console.WriteLine("Get streaming group");
            StreamingGroup = new StreamingGroup(group.Locations);

            //Connect to the streaming group
            Console.WriteLine("Connect to group");
            await Client.Connect(group.Id);

            Console.WriteLine("Done !");
            BaseLayer = StreamingGroup.GetNewLayer(true);
            Ready = true;

            cancellationTokenSource = new CancellationTokenSource();

            //Start auto updating this entertainment group
            await Client.AutoUpdate(StreamingGroup, cancellationTokenSource.Token, 50);
            
        }

        public static void Dispose()
        {
            cancellationTokenSource.Cancel();
            Client.Close();
        }

        [Serializable]
        private struct HueCredential
        {
            public string Username;
            public string Key;
        }

    }
}