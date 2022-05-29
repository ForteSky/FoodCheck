using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using FoodCheck.Attributes;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.ClientState.Party;
using Dalamud.Hooking;

namespace FoodCheck
{
    public class Plugin : IDalamudPlugin
    {
        private readonly DalamudPluginInterface pluginInterface;
        private readonly PartyList partyList;
        private readonly ChatGui chat;
        private readonly SigScanner _sig;

        private readonly PluginCommandManager<Plugin> commandManager;
        private readonly Configuration config;
        private readonly WindowSystem windowSystem;
        private IntPtr _countdownPtr;
        private readonly CountdownTimer _countdownTimer;
        private Hook<CountdownTimer> _countdownTimerHook;
        private Stopwatch _stopwatch;
        
        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr CountdownTimer(ulong p1);

        public string Name => "Your Plugin's Display Name";

        public Plugin(
            DalamudPluginInterface pi,
            CommandManager commands,
            PartyList partyList,
            ChatGui chat,
            SigScanner sig)
        {
            this.pluginInterface = pi;
            this.partyList = partyList;
            this.chat = chat;
            this._sig = sig;
            this._countdownTimer = CountdownTimerFunc;
            this._stopwatch = new Stopwatch();
            HookCountdownPointer();

            // Get or create a configuration object
            this.config = (Configuration)this.pluginInterface.GetPluginConfig()
                          ?? this.pluginInterface.Create<Configuration>();

            // Initialize the UI
            this.windowSystem = new WindowSystem(typeof(Plugin).AssemblyQualifiedName);

            var window = this.pluginInterface.Create<PluginWindow>();
            if (window is not null)
            {
                this.windowSystem.AddWindow(window);
            }

            this.pluginInterface.UiBuilder.Draw += this.windowSystem.Draw;

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);
        }
        
        private IntPtr CountdownTimerFunc(ulong value)
        {
            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
            }
            else
            {
                if(_stopwatch.ElapsedMilliseconds < 30000)
                    return _countdownTimerHook.Original(value);
                _stopwatch.Restart();
            }
            bool first = true;
            foreach (var partyMember in partyList)
            {
                if (partyMember.Statuses.FirstOrDefault(status => status.GameData.Name == "Well Fed") == null)
                {
                    if (first)
                    {
                        this.chat.Print($"FOOD CHECK!!!");
                        first = false;
                    }
                    this.chat.Print($"{partyMember.Name}");
                }
            }
            return _countdownTimerHook.Original(value);
        }

        // [Command("/example1")]
        // [HelpMessage("Example help message.")]
        // public void ExampleCommand1(string command, string args)
        // {
        //     // You may want to assign these references to private variables for convenience.
        //     // Keep in mind that the local player does not exist until after logging in.
        //     // var world = this.clientState.LocalPlayer?.CurrentWorld.GameData;
        //     // this.chat.Print($"Hello, {world?.Name}!");
        //     // PluginLog.Log("Message sent successfully.");
        //     bool first = true;
        //     foreach (var partyMember in partyList)
        //     {
        //         if (partyMember.Statuses.FirstOrDefault(status => status.GameData.Name == "Well Fed") == null)
        //         {
        //             if (first)
        //             {
        //                 this.chat.Print($"FOOD CHECK!");
        //                 first = false;
        //             }
        //             this.chat.Print($"{partyMember.Name}");
        //         }
        //     }
        // }
        
        private void HookCountdownPointer()
        {
            _countdownPtr = _sig.ScanText("48 89 5C 24 ?? 57 48 83 EC 40 8B 41");
            try
            {
                _countdownTimerHook = new Hook<CountdownTimer>(_countdownPtr, _countdownTimer);
                _countdownTimerHook.Enable();
            }
            catch (Exception e)
            {
                PluginLog.Error("Could not hook to timer\n" + e);
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            this.pluginInterface.SavePluginConfig(this.config);

            this.pluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
            this.windowSystem.RemoveAllWindows();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
