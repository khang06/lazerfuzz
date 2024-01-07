using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing.Drawables.Steps;
using osu.Framework.Testing.Input;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;
using SharpFuzz;
using static osu.Framework.Threading.ScheduledDelegate;

#nullable disable

namespace lazerfuzz;

public partial class FuzzerGame : OsuGameBase
{
    private FuzzerRunner runner;

    protected override void LoadAsyncComplete()
    {
        Add(runner = new FuzzerRunner());
    }

    public void Initialize() => runner.Initialize();
    public void Run(Stream input, bool log = false) => runner.Run(input, log);

    protected override int UnhandledExceptionsBeforeCrash => 0;

    public partial class FuzzerTestScene : EditorTestScene
    {
        public FuzzerTestScene()
        {
            Stack.ScreenExited += stopOnChange;
            Stack.ScreenPushed += stopOnChange;
        }

        public static void Exit()
        {
            if (FuzzerRunner.FuzzMode)
                Native.fast_exit(); // causes Nyx to instantly reload the VM
            else
                Exited = true;
        }

        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();
        public new ManualInputManager InputManager => base.InputManager;
        public new Container<Drawable> Content => base.Content;
        public static bool Exited { get; set; } // this kinda sucks
        private static ScreenChangedDelegate stopOnChange = (_, _) => Exit();
    }

    public partial class FuzzerRunner : CompositeDrawable
    {
        [Resolved]
        private GameHost host { get; set; }
        private FuzzerTestScene scene;

        // REMEMBER TO UPDATE nyx_net_spec.py IF THESE CHANGE!!!!
        private Key[] validKeys;
        private static readonly MouseButton[] mouseButtons = { MouseButton.Left, MouseButton.Right };
        private static readonly Key[] modifierKeys = { Key.LShift, Key.LAlt, Key.LControl };

        public static bool FuzzMode { get; private set; }

        public FuzzerRunner()
        {
            Key[] blacklist = { Key.LShift, Key.LAlt, Key.LControl, Key.Unknown, Key.LastKey };
            RelativeSizeAxes = Axes.Both;
            validKeys = Enum.GetValues<Key>().Except(blacklist).ToArray();
        }

        public void Initialize()
        {
            if (scene != null)
            {
                var sched = Schedule(() => RemoveInternal(scene, true));
                while (sched.State != RunState.Complete && host.ExecutionState == ExecutionState.Running)
                    Thread.Sleep(1);
            }

            scene = new FuzzerTestScene();
            bool completed = false;

            int actionIndex = 0;
            int actionRepetition = 0;
            var loadableStep = () => actionIndex >= 0 ? scene.StepsContainer.Children.ElementAtOrDefault(actionIndex) as StepButton : null;

            void runNextStep()
            {
                loadableStep()?.PerformStep();
                actionRepetition++;

                if (actionRepetition > (loadableStep()?.RequiredRepetitions ?? 1) - 1)
                {
                    actionIndex++;
                    Console.WriteLine($"Running step {loadableStep()?.Text ?? "none"}");
                    actionRepetition = 0;
                }

                if (actionIndex > scene.StepsContainer.Children.Count - 1 || loadableStep() == null)
                {
                    Console.WriteLine("completed");
                    completed = true;
                    return;
                }

                Scheduler.Add(runNextStep);
            };

            Schedule(() =>
            {
                AddInternal(scene);

                scene.SetUpSteps();
                runNextStep();
            });

            while (!completed && host.ExecutionState == ExecutionState.Running)
                Thread.Sleep(1);
        }

        void logStep(string step)
        {
            if (!FuzzMode)
                Console.WriteLine($"AddStep(\"{step}\", () => {step});");
        }

        public void Run(Stream input, bool log)
        {
            FuzzMode = input == null;
            if (FuzzMode)
            {
                Console.WriteLine("we running now, waiting a bit for things to settle");
                Thread.Sleep(1000);
            }

            FuzzerTestScene.Exited = false;
            bool[] down = new bool[mouseButtons.Length + modifierKeys.Length];
            Action<Stream> fuzzClosure = stream => {
                using var reader = new BinaryReader(stream);
                void inputClosure()
                {
                    uint cmd = 0;
                    uint data = 0;
                    try
                    {
                        cmd = reader.ReadUInt32();
                        data = reader.ReadUInt32();
                    }
                    catch (Exception ex) when (ex is EndOfStreamException || ex is ObjectDisposedException)
                    {
                        if (log)
                            Console.WriteLine("done");
                        FuzzerTestScene.Exit();
                        return;
                    }
                    switch (cmd)
                    {
                        case 0:
                            var quad = scene.Content.ScreenSpaceDrawQuad;
                            var pos = new Vector2((data & 0xFFFF) / 65535.0f * quad.Width, (data >> 16) / 65535.0f * quad.Height);
                            scene.InputManager.MoveMouseTo(pos);
                            logStep($"InputManager.MoveMouseTo(new Vector2({pos.X}f, {pos.Y}f))");
                            break;
                        case 1:
                            if (data < down.Length && !down[data])
                            {
                                if (data < mouseButtons.Length)
                                {
                                    scene.InputManager.PressButton(mouseButtons[data]);
                                    logStep($"InputManager.PressButton(MouseButton.{mouseButtons[data]})");
                                }
                                else
                                {
                                    scene.InputManager.PressKey(modifierKeys[data - mouseButtons.Length]);
                                    logStep($"InputManager.PressKey(Key.{modifierKeys[data - mouseButtons.Length]})");
                                }
                                down[data] = true;
                            }
                            else
                            {
                                FuzzerTestScene.Exit();
                                return;
                            }
                            break;
                        case 2:
                            if (data < down.Length && down[data])
                            {
                                if (data < mouseButtons.Length)
                                {
                                    scene.InputManager.ReleaseButton(mouseButtons[data]);
                                    logStep($"InputManager.ReleaseButton(MouseButton.{mouseButtons[data]})");
                                }
                                else
                                {
                                    scene.InputManager.ReleaseKey(modifierKeys[data - mouseButtons.Length]);
                                    logStep($"InputManager.ReleaseKey(Key.{modifierKeys[data - mouseButtons.Length]})");
                                }
                                down[data] = false;
                            }
                            else
                            {
                                FuzzerTestScene.Exit();
                                return;
                            }
                            break;  
                        case 3:
                            if (data < validKeys.Length)
                            {
                                scene.InputManager.Key(validKeys[data]);
                                logStep($"InputManager.Key(Key.{validKeys[data]})");
                            }
                            else
                            {
                                FuzzerTestScene.Exit();
                                return;
                            }
                            break;  
                    }

                    if (log)
                        Scheduler.AddDelayed(inputClosure, 500.0);
                    else
                        Schedule(inputClosure);
                }
                Schedule(inputClosure);
                while (!FuzzerTestScene.Exited && host.ExecutionState == ExecutionState.Running)
                    Thread.Sleep(1);
            };

            if (FuzzMode)
                Fuzzer.RunNyx(fuzzClosure);
            else
                fuzzClosure(input!);
        }
    }

    protected override void InitialiseFonts()
    {
        // skip fonts load as it's not required for testing purposes.
    }
}
