using Daim.Xms.Pcp;
using MQTTnet;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using PcpStatus = Daim.Xms.Pcp.PcpStatus<object, object>;

namespace Manipulator
{
    public partial class MainWindow : Window
    {
        private string _currentClient = "xms";
        private IMqttClient? _mqttClient;
        private readonly string[] _ports = ["s1", "s2"];

        // 병 이동 애니메이션 관련 변수
        private List<UIElement> s1Bottles;
        private List<UIElement> s2Bottles;
        private int transferredBottles = 0;
        private bool isTransferInProgress = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeMqttClients();
            _ = ConnectMqttClient();
            InitializeBottles();
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

        private void InitializeBottles() {
            s1Bottles = [];
            s2Bottles = [];
        }

        private void S1_L_Click(object sender, RoutedEventArgs e) {
            SendPcpStatus("s1", PcpTransferState.L);
            StartBottleTransferProcess();
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

        public async void StartBottleTransferProcess() {
            if (isTransferInProgress) return;

            isTransferInProgress = true;
            transferredBottles = 0;

            // 모든 버튼 비활성화
            DisableAllButtons();

            // S1에 병 4개가 들어있는 박스 그리기
            DrawS1BoxWithBottles();

            // S2에 빈 박스 그리기
            DrawS2EmptyBox();

            // 병을 하나씩 이동 (4개)
            for (int i = 0; i < 4; i++) {
                await TransferBottleFromS1ToS2(i);
                await Task.Delay(500); // 1초 대기
            }

            // 모든 병이 이동 완료되면 S2의 U 버튼 활성화
            S1_L.IsEnabled = true;
            S2_U.IsEnabled = true;
            isTransferInProgress = false;

            MessageBox.Show("모든 병 이동 완료! S2의 U 버튼이 활성화되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DrawS1BoxWithBottles() {
            // 기존 병들 제거
            foreach (var bottle in s1Bottles) {
                if (S1Container.Children.Contains(bottle))
                    S1Container.Children.Remove(bottle);
            }
            s1Bottles.Clear();

            // 병 4개 생성 (2x2 배치)
            double[] xPositions = { 30, 70, 30, 70 }; // 왼쪽, 오른쪽, 왼쪽, 오른쪽
            double[] yPositions = { 40, 40, 80, 80 }; // 위, 위, 아래, 아래

            for (int i = 0; i < 4; i++) {
                var bottle = CreateBottle(false); // 스티커 없는 병
                Canvas.SetLeft(bottle, xPositions[i]);
                Canvas.SetTop(bottle, yPositions[i]);

                S1Container.Children.Add(bottle);
                s1Bottles.Add(bottle);
            }
        }

        private void DrawS2EmptyBox() {
            foreach (var bottle in s2Bottles) {
                if (S2Container.Children.Contains(bottle))
                    S2Container.Children.Remove(bottle);
            }
            s2Bottles.Clear();
        }

        private static UIElement CreateBottle(bool withSticker) {
            // 병 모양 Path 생성
            var bottlePath = new Path {
                Width = 20,
                Height = 30,
                Stretch = Stretch.Fill,
                Fill = new LinearGradientBrush(Colors.LightBlue, Colors.LightBlue, 90),
                Stroke = Brushes.DarkBlue,
                StrokeThickness = 1,
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    Color = Colors.Gray,
                    Direction = 315,
                    ShadowDepth = 2,
                    Opacity = 0.5
                },
                Data = Geometry.Parse("M 8,0 L 12,0 L 12,8 L 13,9 C 14,10 15,11 16,12 L 17,14 L 17,26 C 17,28 15,29 13,29 L 7,29 C 5,29 3,28 3,26 L 3,14 L 4,12 C 5,11 6,10 7,9 L 8,8 Z")
            };

            if (withSticker) {
                // 병과 스티커를 담을 캔버스
                var canvas = new Canvas {
                    Width = 20,
                    Height = 30
                };

                // 병 추가
                canvas.Children.Add(bottlePath);

                // 흰색 동그란 스티커
                var sticker = new Ellipse {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.Orange,
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                };

                // 스티커를 병 몸통 중앙에 위치
                Canvas.SetLeft(sticker, 6);  // (20-8)/2 = 6
                Canvas.SetTop(sticker, 15);  // 병 몸통 중앙
                canvas.Children.Add(sticker);

                return canvas;
            }

            return bottlePath;
        }

        private async Task TransferBottleFromS1ToS2(int bottleIndex) {
            if (bottleIndex >= s1Bottles.Count) return;

            var bottle = s1Bottles[bottleIndex];
            var startX = Canvas.GetLeft(bottle);
            var startY = Canvas.GetTop(bottle);

            // 1단계: Manipulator 영역으로 이동
            var manipulatorX = 200.0; // Manipulator 영역 중앙
            var manipulatorY = 10.0;

            await AnimateBottleMovement(bottle, startX, startY, manipulatorX, manipulatorY, 1.0);

            // 2단계: Manipulator에서 스티커 붙이기
            await AttachStickerToBottle(bottle);

            // 3단계: S2로 이동
            var s2X = 30.0 + (transferredBottles % 2) * 40; // 2x2 배치
            var s2Y = 40.0 + (transferredBottles / 2) * 40;
            S1Container.Children.Remove(bottle);

            var newBottleWithSticker = CreateBottle(true);
            Canvas.SetLeft(newBottleWithSticker, manipulatorX);
            Canvas.SetTop(newBottleWithSticker, manipulatorY);
            ManipulatorContainer.Children.Add(newBottleWithSticker);

            ManipulatorContainer.Children.Remove(newBottleWithSticker);

            Canvas.SetLeft(newBottleWithSticker, s2X);
            Canvas.SetTop(newBottleWithSticker, s2Y);
            S2Container.Children.Add(newBottleWithSticker);
            s2Bottles.Add(newBottleWithSticker);

            transferredBottles++;
        }

        private static async Task AnimateBottleMovement(UIElement bottle, double fromX, double fromY, double toX, double toY, double durationSeconds) {
            var storyboard = new Storyboard();

            // X 좌표 애니메이션
            var animX = new DoubleAnimation {
                From = fromX,
                To = toX,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                //EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(animX, bottle);
            Storyboard.SetTargetProperty(animX, new PropertyPath("(Canvas.Left)"));

            // Y 좌표 애니메이션
            var animY = new DoubleAnimation {
                From = fromY,
                To = toY,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                //EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(animY, bottle);
            Storyboard.SetTargetProperty(animY, new PropertyPath("(Canvas.Top)"));

            storyboard.Children.Add(animX);
            storyboard.Children.Add(animY);

            var tcs = new TaskCompletionSource<bool>();
            storyboard.Completed += (s, e) => tcs.SetResult(true);

            storyboard.Begin();
            await tcs.Task;
        }

        private static async Task AttachStickerToBottle(UIElement bottle) {
            var flashStoryboard = new Storyboard();
            var flashAnim = new DoubleAnimation {
                From = 1.0,
                To = 0.3,
                Duration = TimeSpan.FromMilliseconds(200),
                RepeatBehavior = new RepeatBehavior(3),
                AutoReverse = true
            };

            Storyboard.SetTarget(flashAnim, bottle);
            Storyboard.SetTargetProperty(flashAnim, new PropertyPath("Opacity"));
            flashStoryboard.Children.Add(flashAnim);

            var tcs = new TaskCompletionSource<bool>();
            flashStoryboard.Completed += (s, e) => tcs.SetResult(true);

            flashStoryboard.Begin();
            await tcs.Task;
        }

        private void EnableAllButtons() {
            S1_L.IsEnabled = true;
            //S1_U.IsEnabled = true;
            //S2_L.IsEnabled = true;
            S2_U.IsEnabled = true;
        }

        // 모든 버튼 비활성화
        private void DisableAllButtons() {
            S1_L.IsEnabled = false;
            //S1_U.IsEnabled = false;
            //S2_L.IsEnabled = false;
            S2_U.IsEnabled = false;
        }

        // 모든 병과 박스 초기화
        private void ResetAllBottles() {
            // S1 컨테이너 초기화
            S1Container.Children.Clear();

            // S2 컨테이너 초기화
            S2Container.Children.Clear();

            // Manipulator 컨테이너 초기화
            ManipulatorContainer.Children.Clear();

            // 리스트 초기화
            s1Bottles.Clear();
            s2Bottles.Clear();

            // 변수 초기화
            transferredBottles = 0;
            isTransferInProgress = false;

            // 박스 색상 초기화
            s1.Background = System.Windows.Media.Brushes.LightGreen;
            s2.Background = System.Windows.Media.Brushes.LightBlue;

            // 모든 버튼 활성화
            EnableAllButtons();
        }
    }
}