﻿using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Text.RegularExpressions;
using cynosure.Model;
using Microsoft.Bot.Builder.Scorables;
using Microsoft.ApplicationInsights;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace cynosure.Dialogs
{
    [Serializable]
    public class RootDispatchDialog : DispatchDialog
    {
        
        [MethodBind]
        [ScorableGroup(0)]
        private async Task ActivityHandler(IDialogContext context, IActivity activity)
        { 
            try
            {
                switch (activity.Type)
                {
                    case ActivityTypes.Message:
                        this.ContinueWithNextGroup();
                        break;
                    case ActivityTypes.ConversationUpdate:
                    case ActivityTypes.ContactRelationUpdate:
                    case ActivityTypes.Typing:
                    case ActivityTypes.DeleteUserData:
                    case ActivityTypes.Ping:
                    default:
                        break;
                }
            }
            catch (Microsoft.Bot.Builder.Internals.Fibers.InvalidNeedException ex)
            {
                var telemetry = new TelemetryClient();
                telemetry.TrackException(ex);
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                Activity reply = ((Activity)activity).CreateReply("Sorry, I'm having some difficulties here. I have to reboot myself. Let's start over");
                await connector.Conversations.ReplyToActivityAsync(reply);
                StateClient stateClient = activity.GetStateClient();
                await stateClient.BotState.DeleteStateForUserAsync(activity.ChannelId, activity.From.Id);
            }
        }
        
        [RegexPattern("start standup|standup|start|stand up")]
        [ScorableGroup(1)]
        public void StartStandup(IDialogContext context, IActivity activity)
        {
            var telemetry = new TelemetryClient();
            telemetry.TrackEvent("Start Standup");
            var standup = GetCurrentStandup(context);
            context.PostAsync(standup.Summary());
        }

        [RegexPattern("(?i)^add (?<item>.*) to (?<list>.*) items.")]
        [RegexPattern("(?i)^add (?<item>.*) to (?<list>.*).")]
        [ScorableGroup(1)]
        public void Add(IDialogContext context, IActivity activity, [Entity("item")] string itemText, [Entity("list")] string list)
        {
            Standup standup;
            if (!context.UserData.TryGetValue(@"standup", out standup))
            {
                context.PostAsync("Not currently in a standup. Use \"start standup\" to get started.");
            }

            int intVal;
            if (IsDoneList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Done.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    foreach (var item in standup.Committed)
                    {
                        standup.Done.Add(item);
                        standup.Committed.Remove(item);
                    }
                }
                else
                {
                    standup.Done.Add(itemText);
                    standup.Committed.Remove(itemText);
                }
            }
            else if (IsCommittedList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Committed.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    foreach (var item in standup.Backlog)
                    {
                        standup.Committed.Add(item);
                        standup.Backlog.Remove(item);
                    }
                }
                else
                {
                    standup.Committed.Add(itemText);
                    standup.Backlog.Remove(itemText);
                }
            }
            else if (IsIssuesList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Issues.ElementAt(intVal - 1);
                }

                standup.Issues.Add(itemText);
            }
            else if (IsBacklogList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Backlog.ElementAt(intVal - 1);
                }

                standup.Backlog.Add(itemText);
            }
            context.UserData.SetValue(@"standup", standup);

            context.PostAsync(standup.Summary());
        }

        [RegexPattern("(?i)^Promote (?<item>.*) from (?<list>.*) items.")]
        [RegexPattern("(?i)^Promote (?<item>.*) from (?<list>.*).")]
        [ScorableGroup(1)]
        public void Promote(IDialogContext context, IActivity activity, [Entity("item")] string itemText, [Entity("list")] string list)
        {
            Standup standup;
            if (!context.UserData.TryGetValue(@"standup", out standup))
            {
                context.PostAsync("Not currently in a standup. Use \"start standup\" to get started.");
            }

            int intVal;
            if (IsCommittedList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Committed.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    for (int i = standup.Committed.Count - 1; i >= 0; i--)
                    {
                        var item = standup.Committed.ElementAt(i);
                        standup.Done.Add(item);
                        standup.Committed.Remove(item);
                    }
                }
                else
                {
                    standup.Done.Add(itemText);
                    standup.Committed.Remove(itemText);
                }
            }
            else if (IsBacklogList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Backlog.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    for (int i = standup.Backlog.Count - 1; i >= 0; i--)
                    {
                        var item = standup.Backlog.ElementAt(i);
                        standup.Committed.Add(item);
                        standup.Backlog.Remove(item);
                    }
                }
                else
                {
                    standup.Committed.Add(itemText);
                    standup.Backlog.Remove(itemText);
                }
            }

            context.UserData.SetValue(@"standup", standup);
            context.PostAsync(standup.Summary());
        }

        [RegexPattern("(?i)^Remove (?<item>.*) from (?<list>.*).")]
        [RegexPattern("(?i)^Demote (?<item>.*) from (?<list>.*).")]
        [ScorableGroup(1)]
        public void Demote(IDialogContext context, IActivity activity, [Entity("item")] string itemText, [Entity("list")] string list)
        {
            Standup standup;
            if (!context.UserData.TryGetValue(@"standup", out standup))
            {
                context.PostAsync("Not currently in a standup. Use \"start standup\" to get started.");
            }


            int intVal;
            if (IsDoneList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Done.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    for (int i = standup.Done.Count - 1; i >= 0; i--)
                    {
                        var item = standup.Done.ElementAt(i);
                        standup.Done.Remove(item);
                        standup.Committed.Add(item);
                    }
                }
                else
                {
                    standup.Done.Remove(itemText);
                    standup.Committed.Add(itemText);
                }
            }
            else if (IsCommittedList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Committed.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    for (int i = standup.Committed.Count - 1; i >= 0; i--)
                    {
                        var item = standup.Committed.ElementAt(i);
                        standup.Committed.Remove(item);
                        standup.Backlog.Add(item);
                    }
                }
                else
                {
                    standup.Committed.Remove(itemText);
                    standup.Backlog.Add(itemText);
                }
            }
            else if (IsIssuesList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Issues.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    standup.Issues = new List<string>();
                }
                else
                {
                    standup.Issues.Remove(itemText);
                }
            }
            else if (IsBacklogList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Backlog.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    standup.Backlog = new List<string>();
                }
                else
                {
                    standup.Backlog.Remove(itemText);
                }
            }

            context.UserData.SetValue(@"standup", standup);
            context.PostAsync(standup.Summary());
        }

        [RegexPattern("(?i)^Delete (?<item>.*) from (?<list>.*).")]
        [ScorableGroup(1)]
        public void Delete(IDialogContext context, IActivity activity, [Entity("item")] string itemText, [Entity("list")] string list)
        {
            Standup standup;
            if (!context.UserData.TryGetValue(@"standup", out standup))
            {
                context.PostAsync("Not currently in a standup. Use \"start standup\" to get started.");
            }

            int intVal;
            if (IsDoneList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Done.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    standup.Done = new List<string>();
                }
                else
                {
                    standup.Done.Remove(itemText);
                }
            }
            else if (IsCommittedList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Committed.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    standup.Committed = new List<string>();
                }
                else
                {
                    standup.Committed.Remove(itemText);
                }
            }
            else if (IsIssuesList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Issues.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    standup.Issues = new List<string>();
                }
                else
                {
                    standup.Issues.Remove(itemText);
                }
            }
            else if (IsBacklogList(list))
            {
                if (int.TryParse(itemText, out intVal))
                {
                    itemText = standup.Backlog.ElementAt(intVal - 1);
                }

                if (isAll(itemText))
                {
                    standup.Backlog = new List<string>();
                }
                else
                {
                    standup.Backlog.Remove(itemText);
                }
            }
            context.UserData.SetValue(@"standup", standup);

            string prompt = "Removed \"" + itemText + "\" from " + list + " items.";
            prompt += "\n\n\n\n" + standup.Summary();
            context.PostAsync(prompt);
        }
        
        [RegexPattern("standup summary|summary|standup report|report")]
        [ScorableGroup(1)]
        public async Task StandupSummary(IDialogContext context, IActivity activity)
        {
            var telemetry = new TelemetryClient();
            telemetry.TrackEvent("Summarize Standup");
            Standup standup;
            if (context.UserData.TryGetValue(@"standup", out standup))
            {
                await context.PostAsync(standup.Summary());
            }
            else
            {
                await context.PostAsync("There is no standup data right now. You can 'start standup' if you like");
            }
            context.Done(true);
        }

        [RegexPattern("help")]
        [ScorableGroup(2)]
        public async Task Help(IDialogContext context, IActivity activity)
        {
            await this.DefaultAsync(context, activity);
        }

        [RegexPattern("version")]
        [ScorableGroup(2)]
        public async Task Version(IDialogContext context, IActivity activity)
        {
            Assembly thisAssem = typeof(RootDispatchDialog).Assembly;
            AssemblyName thisAssemName = thisAssem.GetName();

            await context.PostAsync(thisAssemName.ToString());
        }

        [MethodBind]
        [ScorableGroup(2)]
        public async Task DefaultAsync(IDialogContext context, IActivity activity)
        {
            var telemetry = new TelemetryClient();
            try
            {
                telemetry.TrackEvent("Display Help");
                context.Call(new HelpDialog(), AfterDialog);
            }
            catch (Microsoft.Bot.Builder.Internals.Fibers.InvalidNeedException ex)
            {
                telemetry.TrackException(ex);
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                Activity reply = ((Activity)activity).CreateReply("Sorry, I'm having some difficulties here. I have to reboot myself. Let's start over.");
                await connector.Conversations.ReplyToActivityAsync(reply);
                StateClient stateClient = activity.GetStateClient();
                await stateClient.BotState.DeleteStateForUserAsync(activity.ChannelId, activity.From.Id);
                telemetry.TrackEvent("Cleared users state.");
            }
        }

        [RegexPattern("hello|hi")]
        [ScorableGroup(1)]
        public async Task Hello(IDialogContext context, IActivity activity)
        {
            var telemetry = new TelemetryClient();
            telemetry.TrackEvent("Say Hi");
            await context.PostAsync(@"Hello, I'm Cynosure. Say 'help' to learn more about what I can do.");
            context.Done(true);
        }

        private bool isAll(string input)
        {
            string[] allWords = new string[] { "all", "everything" };

            bool all = false;
            foreach (string word in allWords)
            {
                all = all || (input.ToLower() == word);
            }
            return all;
        }

        private static bool IsDoneList(string list)
        {
            List<string> synonyms = new List<string>() { "done", "complete", "completed" };
            bool isList = false;
            foreach (string synonym in synonyms)
            {
                isList = isList || list.ToLower().Equals(synonym);
            }
            return isList;
        }

        private static bool IsCommittedList(string list)
        {
            List<string> synonyms = new List<string>() { "committed", "today", "focus" };
            bool isCommitted = false;
            foreach (string synonym in synonyms)
            {
                isCommitted = isCommitted || list.ToLower().Equals(synonym);
            }
            return isCommitted;
        }

        private static bool IsBacklogList(string list)
        {
            List<string> synonyms = new List<string>() { "backlog", "todo", "fixme", "future" };
            bool isBacklog = false;
            foreach (string synonym in synonyms)
            {
                isBacklog = isBacklog || list.ToLower().Equals(synonym);
            }
            return isBacklog;
        }

        private static bool IsIssuesList(string list)
        {
            List<string> synonyms = new List<string>() { "issue", "issues", "needs", "barriers" };
            bool isIssues = false;
            foreach (string synonym in synonyms)
            {
                isIssues = isIssues || list.ToLower().Equals(synonym);
            }
            return isIssues;
        }

        private Standup GetCurrentStandup(IDialogContext context)
        {
            Standup standup;
            if (!context.UserData.TryGetValue(@"standup", out standup))
            {
                standup = new Standup();
                context.UserData.SetValue<Standup>(@"standup", standup);
            }
            return standup;
        }

        private static async Task AfterDialog(IDialogContext context, IAwaitable<object> result)
        {
            context.Done<object>(null);
        }
        
    }
}