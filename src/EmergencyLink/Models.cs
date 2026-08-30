using System;
using System.Collections.Generic;

namespace EmergencyLink
{
    public static class RoleNames
    {
        public const string Manager = "manager";
        public const string Organizer = "organizer";
        public const string Player = "player";
        public const string Teammate = "teammate";

        public static string Display(string role)
        {
            if (role == Manager) return "管理者";
            if (role == Organizer) return "主办方";
            if (role == Player) return "选手-比赛";
            if (role == Teammate) return "选手-队友";
            return role;
        }

        public static string FromDisplay(string display)
        {
            if (display == "管理者") return Manager;
            if (display == "主办方") return Organizer;
            if (display == "选手-比赛") return Player;
            if (display == "选手-队友") return Teammate;
            return display;
        }

        public static bool CanApprove(string role)
        {
            return role == Manager || role == Organizer;
        }

        public static bool CanManageRoom(string role)
        {
            return role == Manager || role == Organizer;
        }
    }

    public static class PhaseNames
    {
        public const string Preparation = "preparation";
        public const string PreMatchTest = "prematch_test";
        public const string InMatch = "in_match";
        public const string Ended = "ended";

        public static string Display(string phase)
        {
            if (phase == Preparation) return "准备中";
            if (phase == PreMatchTest) return "赛前测试";
            if (phase == InMatch) return "比赛中";
            if (phase == Ended) return "已结束";
            return phase;
        }
    }

    public static class AlertTypes
    {
        public const string Test = "test";
        public const string Official = "official";

        public static string Display(string type)
        {
            if (type == Test) return "测试提醒";
            if (type == Official) return "正式告警";
            return type;
        }
    }

    public static class BatchStatus
    {
        public const string Active = "active";
        public const string Approved = "approved";
        public const string Closed = "closed";

        public static string Display(string status)
        {
            if (status == Active) return "待处理";
            if (status == Approved) return "已同意";
            if (status == Closed) return "已关闭";
            return status;
        }
    }

    public sealed class AppConfig
    {
        public string RoomName;
        public string Password;
        public int Port;
        public int MaxOfficialCalls;
        public int BatchWindowSeconds;

        public static AppConfig CreateDefault()
        {
            AppConfig config = new AppConfig();
            config.RoomName = "match-room";
            config.Password = "123456";
            config.Port = 5050;
            config.MaxOfficialCalls = 3;
            config.BatchWindowSeconds = 30;
            return config;
        }

        public AppConfig Clone()
        {
            AppConfig config = new AppConfig();
            config.RoomName = RoomName;
            config.Password = Password;
            config.Port = Port;
            config.MaxOfficialCalls = MaxOfficialCalls;
            config.BatchWindowSeconds = BatchWindowSeconds;
            return config;
        }
    }

    public sealed class MemberView
    {
        public string Id;
        public string Name;
        public string Role;
        public DateTime LastSeen;

        public override string ToString()
        {
            return RoleNames.Display(Role) + " | " + Name + " | 在线";
        }
    }

    public sealed class AlertView
    {
        public string Id;
        public string Type;
        public string Target;
        public string Status;
        public int Count;
        public string AckBy;
        public string ApprovedBy;
        public string Initiators;
        public bool IsOverLimit;
        public DateTime UpdatedAt;

        public override string ToString()
        {
            string label = AlertTypes.Display(Type) + " | " + Target + " | " + BatchStatus.Display(Status);
            label += " | 提醒" + Count.ToString() + "次";
            if (!String.IsNullOrEmpty(AckBy)) label += " | 已回执:" + AckBy;
            if (!String.IsNullOrEmpty(ApprovedBy)) label += " | 已同意:" + ApprovedBy;
            if (IsOverLimit) label += " | 超额";
            return label;
        }
    }

    internal sealed class AlertBatch
    {
        public string Id;
        public string Type;
        public string Target;
        public string Status;
        public int Count;
        public bool IsOverLimit;
        public DateTime CreatedAt;
        public DateTime UpdatedAt;
        public bool DeliveredToPlayer;
        public string DeliveredBy;
        public DateTime? DeliveredAt;
        public string AckBy;
        public DateTime? AckAt;
        public string ApprovedBy;
        public string ApprovedByRole;
        public DateTime? ApprovedAt;
        public readonly List<string> Initiators = new List<string>();
    }
}
