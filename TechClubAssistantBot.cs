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
        private const double LUIS_INTENT_THRESHOLD = 0.2d;

        private readonly string[] _meetingRooms = { "krzyki", "country", "fabryczna", "jazz", "psie pole", "rock", "soul", "values" };

        private readonly DialogSet dialogs;

        public TechClubAssistantBot()
        {
            dialogs = new DialogSet();
            dialogs.Add("None", new WaterfallStep[] { DefaultDialog });
            dialogs.Add("Greeting", new WaterfallStep[] { GreetingDialog });
            dialogs.Add("BookMeetingRoom", new WaterfallStep[] 
            {
                BookingRoomStart, BookingRoomTime, BookingRoomDuration, BookingRoomConfirmation, BookingRoomFinish
            });

            dialogs.Add("MeetingRoomPrompt", new ChoicePrompt(Culture.English));
            dialogs.Add("TimePrompt", new TextPrompt(TimeValidator));
            dialogs.Add("DurationPrompt", new TextPrompt(DurationValidator));
            dialogs.Add("ConfirmPrompt", new ChoicePrompt(Culture.English));


            dialogs.Add("GetAvailableRoomsForSpecificTime", new WaterfallStep[] { GetAvailableRoomsStart });
        }

        private async Task TimeValidator(ITurnContext context, Microsoft.Bot.Builder.Prompts.TextResult result)
        {
            if (string.IsNullOrWhiteSpace(result.Value))
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

            await dialogContext.Context.SendActivity($"Booking meeting room...");

            if (String.IsNullOrEmpty(state.MeetingRoom))
            {
                var choices = _meetingRooms.Select(r => new Choice() { Value = r }).ToList();
                await dialogContext.Prompt("MeetingRoomPrompt", "Select meeting room: ", new ChoicePromptOptions() { Choices = choices });
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
                await dialogContext.Prompt("TimePrompt", "Enter date and time: ");
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

            if (args is Microsoft.Bot.Builder.Prompts.TextResult textResult)
            {
                state.Time = textResult.Value;
            }

            if (String.IsNullOrEmpty(state.Duration))
            {
                await dialogContext.Prompt("DurationPrompt", "Enter duration: ");
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


            if (args is Microsoft.Bot.Builder.Prompts.TextResult textResult)
            {
                state.Duration = textResult.Value;
            }

            var choices = new List<Choice> { new Choice() { Value = "Confirm" }, new Choice() { Value = "Reject" } };
            await dialogContext.Prompt("ConfirmPrompt", "Please confirm your booking", new ChoicePromptOptions() { Choices = choices });
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
                await dialogContext.Context.SendActivity($"Your booking '{state.MeetingRoom}' was processed.");
                //Process booking
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
            return dialogContext.Context.SendActivity("#### Welcome to Meeting Room Booking Bot\n\nI can help you **book** of **list** available MR");
        }


        private static void FillBookingStateFromLuisResult(TechClubAssistantBotState state, LuisResult result)
        {
            EntityRecommendation meetingRoomEntity = result.Entities.Where(e => e.Type == "Meeting Room").FirstOrDefault();
            state.MeetingRoom = ((meetingRoomEntity?.Resolution?["values"]) as List<object>)?.FirstOrDefault()?.ToString();

            EntityRecommendation durationEntity = result.Entities.Where(e => e.Type == "builtin.datetimeV2.duration").FirstOrDefault();
            state.Duration = (((durationEntity?.Resolution?["values"]) as List<object>)?.FirstOrDefault() as Dictionary<string, object>)?["value"]?.ToString();

            EntityRecommendation timeEntity = result.Entities.Where(e => e.Type == "builtin.datetimeV2.datetime").FirstOrDefault();
            state.Time = (((timeEntity?.Resolution?["values"]) as List<object>)?.FirstOrDefault() as Dictionary<string, object>)?["value"]?.ToString();
        }




        private Task GetAvailableRoomsStart(DialogContext dialogContext, object args, SkipStepFunction next)
        {
            return dialogContext.Context.SendActivity("Getting available rooms...");
        }

        public async Task OnTurn(ITurnContext context)
        {
            if (context.Activity.Type == ActivityTypes.ConversationUpdate && context.Activity.MembersAdded.FirstOrDefault()?.Id == context.Activity.Recipient.Id)
            {
                await context.SendActivity("#### Welcome to Meeting Room Booking Bot\n\nI can help you **book** of **list** available MR");
            }
            else if (context.Activity.Type == ActivityTypes.Message)
            {
                var state = context.GetConversationState<Dictionary<string, object>>();
                var dialogContext = dialogs.CreateContext(context, state);

                var utterance = context.Activity.Text.ToLowerInvariant();
                if (utterance == "cancel")
                {
                    if (dialogContext.ActiveDialog != null)
                    {
                        await context.SendActivity("Ok... Cancelled");
                        dialogContext.EndAll();
                    }
                    else
                    {
                        await context.SendActivity("Nothing to cancel.");
                    }
                }

                if (!context.Responded)
                {
                    await dialogContext.Continue();

                    if (!context.Responded)
                    {
                        var luisResult = context.Services.Get<RecognizerResult>(LuisRecognizerMiddleware.LuisRecognizerResultKey);
                        var (intent, score) = luisResult.GetTopScoringIntent();
                        var intentResult = score > LUIS_INTENT_THRESHOLD ? intent : "None";                       

                        await dialogContext.Begin(intent, luisResult.Properties);
                    }
                }
            }
        }
    }
}
