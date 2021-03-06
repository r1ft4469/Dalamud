using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Internal.Libc;
using Serilog;

namespace Dalamud.Game.Command {
    /// <summary>
    /// This class manages registered in-game slash commands.
    /// </summary>
    public sealed class CommandManager {
        private readonly Dalamud dalamud;

        private readonly Dictionary<string, CommandInfo> commandMap = new Dictionary<string, CommandInfo>();

        /// <summary>
        /// Read-only list of all registered commands.
        /// </summary>
        public ReadOnlyDictionary<string, CommandInfo> Commands =>
            new ReadOnlyDictionary<string, CommandInfo>(this.commandMap);

        private readonly Regex commandRegexEn =
            new Regex(@"^The command (?<command>.+) does not exist\.$", RegexOptions.Compiled);

        private readonly Regex commandRegexJp = new Regex(@"^そのコマンドはありません。： (?<command>.+)$", RegexOptions.Compiled);

        private readonly Regex commandRegexDe =
            new Regex(@"^„(?<command>.+)“ existiert nicht als Textkommando\.$", RegexOptions.Compiled);

        private readonly Regex commandRegexFr =
            new Regex(@"^La commande texte “(?<command>.+)” n'existe pas\.$",
                      RegexOptions.Compiled);

        private readonly Regex currentLangCommandRegex;


        public CommandManager(Dalamud dalamud, ClientLanguage language) {
            this.dalamud = dalamud;

            switch (language) {
                case ClientLanguage.Japanese:
                    this.currentLangCommandRegex = this.commandRegexJp;
                    break;
                case ClientLanguage.English:
                    this.currentLangCommandRegex = this.commandRegexEn;
                    break;
                case ClientLanguage.German:
                    this.currentLangCommandRegex = this.commandRegexDe;
                    break;
                case ClientLanguage.French:
                    this.currentLangCommandRegex = this.commandRegexFr;
                    break;
            }

            dalamud.Framework.Gui.Chat.OnCheckMessageHandled += OnCheckMessageHandled;
        }

        private void OnCheckMessageHandled(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            if (type == XivChatType.ErrorMessage && senderId == 0) {
                var cmdMatch = this.currentLangCommandRegex.Match(message.TextValue).Groups["command"];
                if (cmdMatch.Success) {
                    // Yes, it's a chat command.
                    var command = cmdMatch.Value;
                    if (ProcessCommand(command)) isHandled = true;
                }
            }
        }

        /// <summary>
        /// Process a command in full.
        /// </summary>
        /// <param name="content">The full command string.</param>
        /// <returns>True if the command was found and dispatched.</returns>
        public bool ProcessCommand(string content) {
            string command;
            string argument;

            var speratorPosition = content.IndexOf(' ');
            if (speratorPosition == -1 || speratorPosition + 1 >= content.Length) {
                // If no space was found or ends with the space. Process them as a no argument
                command = content;
                argument = string.Empty;
            } else {
                // e.g.)
                // /testcommand arg1
                // => Total of 17 chars
                // => command: 0-12 (12 chars)
                // => argument: 13-17 (4 chars)
                // => content.IndexOf(' ') == 12
                command = content.Substring(0, speratorPosition);

                var argStart = speratorPosition + 1;
                argument = content.Substring(argStart, content.Length - argStart);
            }

            if (!this.commandMap.TryGetValue(command, out var handler)) // Commad was not found.
                return false;

            DispatchCommand(command, argument, handler);
            return true;
        }

        /// <summary>
        /// Dispatch the handling of a command.
        /// </summary>
        /// <param name="command">The command to dispatch.</param>
        /// <param name="argument">The provided arguments.</param>
        /// <param name="info">A <see cref="CommandInfo"/> object describing this command.</param>
        public void DispatchCommand(string command, string argument, CommandInfo info) {
            try {
                info.Handler(command, argument);
            } catch (Exception ex) {
                Log.Error(ex, "Error while dispatching command {CommandName} (Argument: {Argument})", command,
                          argument);
            }
        }

        /// <summary>
        /// Add a command handler, which you can use to add your own custom commands to the in-game chat.
        /// </summary>
        /// <param name="command">The command to register.</param>
        /// <param name="info">A <see cref="CommandInfo"/> object describing the command.</param>
        /// <returns>If adding was successful.</returns>
        public bool AddHandler(string command, CommandInfo info) {
            if (info == null) throw new ArgumentNullException(nameof(info), "Command handler is null.");

            try {
                this.commandMap.Add(command, info);
                return true;
            } catch (ArgumentException) {
                Log.Error("Command {CommandName} is already registered.", command);
                return false;
            }
        }

        /// <summary>
        /// Remove a command from the command handlers.
        /// </summary>
        /// <param name="command">The command to remove.</param>
        /// <returns>If the removal was successful.</returns>
        public bool RemoveHandler(string command) {
            return this.commandMap.Remove(command);
        }
    }
}
