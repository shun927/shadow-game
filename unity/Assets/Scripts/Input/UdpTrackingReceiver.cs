using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace OomiyaFes.Input
{
    public sealed class UdpTrackingReceiver : MonoBehaviour
    {
        [SerializeField] private int port = 5005;
        [SerializeField] private float timeoutSeconds = 0.5f;

        private UdpClient client;
        private Thread receiveThread;
        private volatile bool running;
        private readonly object sync = new object();
        private TrackingMessage latest;
        private DateTime lastReceiveUtc;

        public bool HasPose { get; private set; }
        public bool IsConnected
        {
            get
            {
                lock (sync)
                {
                    return HasPose && (DateTime.UtcNow - lastReceiveUtc).TotalSeconds <= timeoutSeconds;
                }
            }
        }
        public TrackingMessage Latest
        {
            get
            {
                lock (sync)
                {
                    return latest;
                }
            }
        }

        private void OnEnable()
        {
            client = new UdpClient(port);
            running = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "AprilTag UDP Receiver"
            };
            receiveThread.Start();
        }

        private void OnDisable()
        {
            running = false;
            client?.Close();
            client = null;
            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(200);
            }
            receiveThread = null;
        }

        private void ReceiveLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, port);
            while (running)
            {
                try
                {
                    var bytes = client.Receive(ref any);
                    var json = Encoding.UTF8.GetString(bytes);
                    var message = JsonUtility.FromJson<TrackingMessage>(json);
                    if (message?.field_xyz_m == null)
                    {
                        continue;
                    }

                    lock (sync)
                    {
                        latest = message;
                        HasPose = true;
                        lastReceiveUtc = DateTime.UtcNow;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (!running)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Avoid Unity API calls from this background thread.
                    Console.WriteLine($"UDP tracking parse failed: {ex.Message}");
                }
            }
        }
    }
}
