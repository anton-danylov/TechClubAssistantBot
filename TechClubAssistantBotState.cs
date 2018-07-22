using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TechClubAssistantBot
{
    public class BaseState : Dictionary<string, object>
    {
        public BaseState(IDictionary<string, object> source)
        {
            if (source != null)
            {
                source.ToList().ForEach(x => this.Add(x.Key, x.Value));
            }
        }

        protected T GetProperty<T>([CallerMemberName]string propName = null)
        {
            if (this.TryGetValue(propName, out object value))
            {
                return (T)value;
            }
            return default(T);
        }

        protected void SetProperty(object value, [CallerMemberName]string propName = null)
        {
            this[propName] = value;
        }
    }

    /// <summary>
    /// Class for storing conversation state. 
    /// </summary>
    public class TechClubAssistantBotState : BaseState
    {
        public TechClubAssistantBotState() : base(null) { }

        public TechClubAssistantBotState(IDictionary<string, object> source) : base(source)
        {
            IsConfirmed = false;
        }

        public string MeetingRoom
        {
            get { return GetProperty<string>(); }
            set { SetProperty(value); }
        }

        public string Time
        {
            get { return GetProperty<string>(); }
            set { SetProperty(value); }
        }

        public string Duration
        {
            get { return GetProperty<string>(); }
            set { SetProperty(value); }
        }

        public bool IsConfirmed
        {
            get { return GetProperty<bool>(); }
            set { SetProperty(value); }
        }
    }
}
