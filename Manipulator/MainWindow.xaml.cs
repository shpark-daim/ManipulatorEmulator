using Daim.Xms.Pcp;
using MQTTnet;
using System.Text.Json;
using System.Windows;
using PcpStatus = Daim.Xms.Pcp.PcpStatus<object, object>;

namespace Manipulator
{
    public partial class MainWindow : Window
    {
        private string _currentClient = "xms";
        private IMqttClient? _mqttClient;
        private readonly string[] _ports = ["s1", "s2"];
        public MainWindow()
        {
            InitializeComponent();
            InitializeMqttClients();
            _ = ConnectMqttClient();
        }

        private void InitializeMqttClients() {
            var mqttFactory = new MqttClientFactory();
            _mqttClient = mqttFactory.CreateMqttClient();

            //_mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
            _mqttClient.ConnectedAsync += async (e) => {

                foreach(var portId in _ports)
                {
                    var topicForXms = Pcp.MakeSubAllCmdTopic(portId);
                    await _mqttClient.SubscribeAsync(topicForXms);

                    var topicForAas = Pcp.MakeSubAllCmdTopic(portId, "aas");
                    await _mqttClient.SubscribeAsync(topicForAas);
                }
            };

            _mqttClient.DisconnectedAsync += async (e) => {};
        }

        private async Task ConnectMqttClient() {
            try {
                if (_mqttClient == null) return;

                if (!_mqttClient.IsConnected) {
                    var options = new MqttClientOptionsBuilder()
                        .WithTcpServer("localhost", 1883) // MQTT 브로커 주소
                        .WithClientId("manipulator" + Guid.NewGuid().ToString()[..8])
                        .WithCleanSession()
                        .Build();

                    await _mqttClient.ConnectAsync(options);
                } else {
                    await _mqttClient.DisconnectAsync();
                }
            } catch (Exception ex) {
                Console.WriteLine($"MQTT 연결 오류: {ex.Message}");
            }
        }

        private void S1_L_Click(object sender, RoutedEventArgs e) {
            SendPcpStatus("s1", PcpTransferState.L);
        }

        private void S2_U_Click(object sender, RoutedEventArgs e) {
            SendPcpStatus("s2", PcpTransferState.U);
        }

        private async void SendPcpStatus(string portId, PcpTransferState state) {
            var status = new PcpStatus(portId, 0, 0, PcpMode.A, PcpDirection.B, [], state, false) { };
            var msg = new MqttApplicationMessageBuilder()
                    .WithTopic(Pcp.MakeStatusTopic(portId, _currentClient))
                    .WithPayload(JsonSerializer.Serialize(status))
                    .Build();
            await _mqttClient!.PublishAsync(msg);
        }

        private void MqttToggle_Checked(object sender, RoutedEventArgs e) {
            _currentClient = "aas";
        }

        private void MqttToggle_Unchecked(object sender, RoutedEventArgs e) {
            _currentClient = "xms";
        }
    }
}