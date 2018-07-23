using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Ai.LUIS;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Cognitive.LUIS.Models;
using Microsoft.Recognizers.Text;

namespace TechClubAssistantBot
{
    public class TechClubAssistantBot : IBot
    {
        private const double LuisIntentTreshold = 0.2d;

        private const string MeetingRoomPrompt = "MeetingRoomPrompt";
        private const string TimePrompt = "TimePrompt";
        private const string DurationPrompt = "DurationPrompt";
        private const string ConfirmPrompt = "ConfirmPrompt";
        private const string GreetingMessage = "#### Welcome to Meeting Room Booking Bot\n\nI can help you **book** of **list** available MR";
        private const string DefaultIntent = "None";
        private const string GreetingIntent = "Greeting";
        private const string BookingIntent = "BookMeetingRoom";

        private readonly string[] _meetingRooms = { "krzyki", "country", "fabryczna", "jazz", "psie pole", "rock", "soul", "values" };
        private readonly IMeetingRoomBookingService _meetingRoomBookingService;
        private readonly DialogSet _dialogs;

        private IEnumerable<string> MeetingRooms => _meetingRooms;

        public TechClubAssistantBot(IMeetingRoomBookingService meetingRoomBookingService)
        {
            _meetingRoomBookingService = meetingRoomBookingService;

            _dialogs = new DialogSet();
            _dialogs.Add(DefaultIntent, new WaterfallStep[] { DefaultDialog });
            _dialogs.Add(GreetingIntent, new WaterfallStep[] { GreetingDialog });
            _dialogs.Add(BookingIntent, new WaterfallStep[] 
            {
                BookingRoomStart, BookingRoomTime, BookingRoomDuration, BookingRoomConfirmation, BookingRoomFinish
            });

            _dialogs.Add(MeetingRoomPrompt, new ChoicePrompt(Culture.English));
            _dialogs.Add(TimePrompt, new DateTimePrompt(Culture.English, DateTimeValidator));
            _dialogs.Add(DurationPrompt, new TextPrompt(DurationValidator));
            _dialogs.Add(ConfirmPrompt, new ChoicePrompt(Culture.English));


            _dialogs.Add("GetAvailableRoomsForSpecificTime", new WaterfallStep[] { GetAvailableRoomsStart });
        }


        private async Task DateTimeValidator(ITurnContext context, Microsoft.Bot.Builder.Prompts.DateTimeResult result)
        {
            if (string.IsNullOrWhiteSpace(result.Text))
            {
                result.Status = Microsoft.Bot.Builder.Prompts.PromptStatus.NotRecognized;
                await context.SendActivity("Please enter correct time");
            }
        }

        private async Task DurationValidator(ITurnContext context, Microsoft.Bot.Builder.Prompts.TextResult result)
        {
            if (string.IsNullOrWhiteSpace(result.Value))
            {
                result.Status = Microsoft.Bot.Builder.Prompts.PromptStatus.NotRecognized;
                await context.SendActivity("Please enter correct duration");
            }
        }

        private async Task BookingRoomStart(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            var state = new TechClubAssistantBotState(dialogContext.ActiveDialog.State);
            FillBookingStateFromLuisResult(state, (args as IDictionary<string, object>)?["luisResult"] as LuisResult);

            dialogContext.ActiveDialog.State = state;

            await dialogContext.Context.SendActivity($"Booking meeting room started");

            if (String.IsNullOrEmpty(state.MeetingRoom))
            {
                var choices = MeetingRooms.Select(r => new Choice() { Value = r }).ToList();
                await dialogContext.Prompt(MeetingRoomPrompt, "Select meeting room: ", new ChoicePromptOptions() { Choices = choices });
            }
            else
            {
                await dialogContext.Continue();
            }
        }

        private async Task BookingRoomTime(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            var state = new TechClubAssistantBotState(dialogContext.ActiveDialog.State);
            dialogContext.ActiveDialog.State = state;


            if (args is Microsoft.Bot.Builder.Prompts.ChoiceResult choiceResult)
            {
                state.MeetingRoom = choiceResult.Value.Value;
            }

            if (String.IsNullOrEmpty(state.Time))
            {
                await dialogContext.Prompt(TimePrompt, "Enter date and time: ", new PromptOptions() { RetryPromptString = "Please enter correct date and time" });
            }
            else
            {
                await dialogContext.Continue();
            }
        }

        private async Task BookingRoomDuration(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            var state = new TechClubAssistantBotState(dialogContext.ActiveDialog.State);
            dialogContext.ActiveDialog.State = state;

            if (args is Microsoft.Bot.Builder.Prompts.DateTimeResult result)
            {
                state.Time = result.Text;
            }

            if (String.IsNullOrEmpty(state.Duration))
            {
                await dialogContext.Prompt(DurationPrompt, "Enter duration: ", new PromptOptions() { RetryPromptString = "Please enter correct duration" });
            }
            else
            {
                await dialogContext.Continue();
            }
        }

        private async Task BookingRoomConfirmation(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            var state = new TechClubAssistantBotState(dialogContext.ActiveDialog.State);
            dialogContext.ActiveDialog.State = state;


            if (args is Microsoft.Bot.Builder.Prompts.TextResult result)
            {
                state.Duration = result.Value;
            }

            string summary = $"Please confirm your booking: **{state.MeetingRoom}** at {state.Time} for {state.Duration}";

            var choices = new List<Choice> { new Choice() { Value = "Confirm" }, new Choice() { Value = "Reject" } };
            await dialogContext.Prompt(ConfirmPrompt, summary, new ChoicePromptOptions() { Choices = choices });
        }

        private async Task BookingRoomFinish(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            var state = new TechClubAssistantBotState(dialogContext.ActiveDialog.State);

            if (args is Microsoft.Bot.Builder.Prompts.ChoiceResult choiceResult)
            {
                state.IsConfirmed = choiceResult.Value.Value == "Confirm";
            }

            if (state.IsConfirmed)
            {
                var bookingResult = await _meetingRoomBookingService.BookMeetingRoomAsync(new BookingRequest() { MeetingRoom = state.MeetingRoom, Time = state.Time, Duration = state.Duration });

                await dialogContext.Context.SendActivity($"Your booking was processed.");
            }
            else
            {
                await dialogContext.Context.SendActivity($"Your booking was cancelled");
            }

            await dialogContext.End();
        }



        private Task DefaultDialog(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            return dialogContext.Context.SendActivity("Sorry, I don't understand");
        }

        private Task GreetingDialog(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            return dialogContext.Context.SendActivity(GreetingMessage);
        }



        private Task GetAvailableRoomsStart(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            var meetingRoomsListing = String.Join("\n\n", MeetingRooms);
            return dialogContext.Context.SendActivity($"Available rooms: \n\n{meetingRoomsListing}");
        }

        public async Task OnTurn(ITurnContext context)
        {
            if (context.Activity.Type == ActivityTypes.ConversationUpdate && context.Activity.MembersAdded.FirstOrDefault()?.Id == context.Activity.Recipient.Id)
            {
                await context.SendActivity(GreetingMessage);
            }
            else if (context.Activity.Type == ActivityTypes.Message)
            {
                var state = context.GetConversationState<Dictionary<string, object>>();
                var dialogContext = _dialogs.CreateContext(context, state);

                if (!context.Responded)
                {
                    await dialogContext.Continue();

                    if (!context.Responded)
                    {
                        var luisResult = context.Services.Get<RecognizerResult>(LuisRecognizerMiddleware.LuisRecognizerResultKey);
                        var (intent, score) = luisResult.GetTopScoringIntent();
                        var intentResult = score > LuisIntentTreshold ? intent : DefaultIntent;                       

                        await dialogContext.Begin(intent, luisResult.Properties);
                    }
                }
            }
        }


        private static void FillBookingStateFromLuisResult(TechClubAssistantBotState state, LuisResult result)
        {
            EntityRecommendation meetingRoomEntity = result.Entities.Where(e => e.Type == "Meeting Room").FirstOrDefault();
            state.MeetingRoom = ((meetingRoomEntity?.Resolution?["values"]) as List<object>)?.FirstOrDefault()?.ToString();

            EntityRecommendation durationEntity = result.Entities.Where(e => e.Type == "builtin.datetimeV2.duration").FirstOrDefault();
            var resolvedDuration = (((durationEntity?.Resolution?["values"]) as List<object>)?.FirstOrDefault() as Dictionary<string, object>)?["value"];
            state.Duration = resolvedDuration != null ? TimeSpan.FromSeconds(Convert.ToDouble(resolvedDuration)).ToString() : null;

            EntityRecommendation timeEntity = result.Entities.Where(e => e.Type == "builtin.datetimeV2.datetime").FirstOrDefault();
            state.Time = (((timeEntity?.Resolution?["values"]) as List<object>)?.FirstOrDefault() as Dictionary<string, object>)?["value"]?.ToString();
        }
    }
}
