using System;
using Q42.HueApi;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;
using System.Linq;
using Q42.HueApi.Models.Bridge;
using System.IO;

namespace Musicolor
{
    public class Hue
    {
        public static StreamingHueClient Client;
        public static StreamingGroup StreamingGroup;
        public static bool Setup = false;
        public static EntertainmentLayer BaseLayer;

        private const string credentialPath = "hue_credentials.json";
        private static HueCredential hueCredential;

        public static async void Register(string ip)
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
            
            Client = new StreamingHueClient(ip, registeredInfos.Username, registeredInfos.StreamingClientKey);

            Console.WriteLine("Get client");

            //Get the entertainment group
            var all = await Client.LocalHueClient.GetEntertainmentGroups();
            var group = all.FirstOrDefault();

            Console.WriteLine("Get entertainment group");

            //Create a streaming group
            StreamingGroup = new StreamingGroup(group.Locations);

            Console.WriteLine("Get streaming group");

            //Connect to the streaming group
            await Client.Connect(group.Id);

            Console.WriteLine("Connect to group");

            BaseLayer = StreamingGroup.GetNewLayer(true);

            Setup = true;

            Console.WriteLine("Done !");

            //Start auto updating this entertainment group
            await Client.AutoUpdate(StreamingGroup, new System.Threading.CancellationToken(), 50);
            
            Console.WriteLine("Stop !");
        }

        [Serializable]
        private struct HueCredential
        {
            public string Username;
            public string Key;
        }

    }
}