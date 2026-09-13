using Maple2.Database.Storage;
using Maple2.Model.Enum;
using Maple2.Model.Error;
using Maple2.Model.Game;
using Maple2.Model.Metadata;
using Maple2.Server.Game.Model;
using Maple2.Server.Game.Packets;
using Maple2.Server.Game.Session;
using Maple2.Server.Game.Util;
using Maple2.Tools.Extensions;
using Serilog;

namespace Maple2.Server.Game.Manager;

public sealed class NpcScriptManager {
    private readonly GameSession session;

    public readonly FieldNpc? Npc;
    private readonly ScriptMetadata? metadata;
    private readonly Dictionary<int, ScriptConditionMetadata>? scriptConditions;
    public SortedDictionary<int, QuestMetadata> Quests = new();
    public JobConditionMetadata? JobCondition;

    public NpcTalkType TalkType;
    public NpcTalkButton Button;

    private readonly CinematicEventScript[] eventScripts = [];

    public ScriptState? State { get; set; }
    public int Index { get; private set; } = 0;
    private readonly ILogger logger = Log.Logger.ForContext<NpcScriptManager>();

    public NpcScriptManager(GameSession session, FieldNpc? npc, ScriptMetadata? metadata, ScriptState? state, NpcTalkType talkType) {
        this.session = session;
        Npc = npc;
        this.metadata = metadata;
        TalkType = talkType;
        State = state;

        if (Npc != null) {
            if (session.ServerTableMetadata.ScriptConditionTable.Entries.TryGetValue(Npc.Value.Id, out Dictionary<int, ScriptConditionMetadata>? scriptConditionMetadata)) {
                scriptConditions = scriptConditionMetadata;
            }
            if (talkType.HasFlag(NpcTalkType.Quest)) {
                Quests = session.Quest.GetAvailableQuests(Npc.Value.Id);
            }

            if (state?.Type == ScriptStateType.Job) {
                JobCondition = session.ServerTableMetadata.JobConditionTable.Entries.GetValueOrDefault(Npc.Value.Id);
            }
        }
    }

    /// <summary>
    /// Used for Event Scripts
    /// </summary>
    public NpcScriptManager(GameSession session, CinematicEventScript[] eventScripts) {
        this.session = session;
        this.eventScripts = eventScripts;
    }

    public void EmpowerEvent() {
        CinematicEventScript script = eventScripts[Index];
        ScriptContent content = script.Contents[Random.Shared.Next(script.Contents.Length - 1)];
        session.Send(NpcTalkPacket.Update(content));
        Index++;
    }

    public bool BeginNpcTalk() {
        if (State == null || Npc == null) {
            session.Send(NpcTalkPacket.Close());
            Npc?.StopTalk();
            return false;
        }
        var dialogue = new NpcDialogue(State.Id, 0, GetButton());

        if (TalkType.HasFlag(NpcTalkType.Quest)) {
            session.Send(QuestPacket.Talk(Npc, Quests.Values));
        }

        session.Send(NpcTalkPacket.Respond(Npc, TalkType, dialogue));
        Npc.Talk();
        ProcessScriptFunction();

        return true;
    }

    public bool BeginQuest() {
        TalkType = NpcTalkType.Quest;
        return QuestRespond();
    }

    private bool QuestRespond() {
        State = GetQuestScriptState(metadata);
        if (State == null || State.Id == 0) {
            session.Send(NpcTalkPacket.Close());
            Npc?.StopTalk();
            return false;
        }
        Button = GetButton();

        var dialogue = new NpcDialogue(State.Id, Index, Button);
        session.Send(NpcTalkPacket.Continue(TalkType, dialogue, metadata!.Id));
        return true;
    }

    public void EnterDialog() {
        // Basic shops are treated slightly different
        if (Npc?.Value.Metadata.Basic.Kind is 1 or > 10 and < 20) {
            TalkType = NpcTalkType.Talk;
            State = null;
            Button = GetButton();
            return;
        }

        EnterTalk();
    }

    public void EnterTalk() {
        if (Npc == null) {
            return;
        }
        ScriptState? scriptState = NpcTalkUtil.GetInitialScriptType(session, ScriptStateType.Script, metadata, Npc);
        if (scriptState == null) {
            return;
        }
        if (scriptState.Type == ScriptStateType.Job) {
            JobCondition = session.ServerTableMetadata.JobConditionTable.Entries.GetValueOrDefault(Npc.Value.Id);
        } else {
            JobCondition = null;
        }

        TalkType = scriptState.Type == ScriptStateType.Script ? NpcTalkType.Talk : NpcTalkType.Dialog;

        State = scriptState;
        Button = GetButton();
    }

    public bool Continue(int pick) {
        ScriptState? nextState = NextState(pick);
        if (nextState == null) {
            session.Send(NpcTalkPacket.Close());
            if (State?.Type == ScriptStateType.Job) {
                PerformJobScript();
            }
            Npc?.StopTalk();
            return false;
        }

        int questId = 0;
        if (metadata!.Type == ScriptType.Quest) {
            questId = metadata.Id;
        }
        if (nextState != State && !metadata.States.ContainsKey(nextState.Id)) {
            session.Send(NpcTalkPacket.Close());
            Npc?.StopTalk();
            return false;
        }

        if (nextState == State) {
            Index++;
        } else {
            Index = 0;
        }

        State = nextState;
        Button = GetButton();

        var dialogue = new NpcDialogue(State.Id, Index, Button);
        ProcessScriptFunction();
        session.Send(NpcTalkPacket.Continue(TalkType, dialogue, questId));
        return true;
    }

    private ScriptState? NextState(int pick) {
        if (State == null) {
            return null;
        }

        CinematicContent? content = State.Contents.ElementAtOrDefault(Index);
        if (content == null) {
            return null;
        }

        if (content.Distractors.Length > 0) {
            CinematicDistractor? distractor = content.Distractors.ElementAtOrDefault(pick);
            if (distractor == null) {
                return null;
            }

            IList<ScriptState> goToScripts = new List<ScriptState>();
            foreach (int goToScriptId in distractor.Goto) {
                if (!metadata!.States.TryGetValue(goToScriptId, out ScriptState? goToScript)) {
                    continue;
                }

                if (scriptConditions == null || !scriptConditions.TryGetValue(goToScript.Id, out ScriptConditionMetadata? scriptConditionMetadata)) {
                    goToScripts.Add(goToScript);
                    continue;
                }

                if (scriptConditionMetadata.ConditionCheck(session, Npc)) {
                    goToScripts.Add(goToScript);
                }
            }

            if (goToScripts.Count > 0) {
                return goToScripts[Random.Shared.Next(goToScripts.Count)];
            }

            IList<ScriptState> goToFailScripts = new List<ScriptState>();
            foreach (int goToFailScriptId in distractor.GotoFail) {
                if (!metadata!.States.TryGetValue(goToFailScriptId, out ScriptState? goToFailScript)) {
                    continue;
                }

                if (scriptConditions == null || !scriptConditions.TryGetValue(goToFailScript.Id, out ScriptConditionMetadata? scriptConditionMetadata)) {
                    goToFailScripts.Add(goToFailScript);
                    continue;
                }

                if (scriptConditionMetadata.ConditionCheck(session, Npc)) {
                    goToFailScripts.Add(goToFailScript);
                }
            }

            if (goToFailScripts.Count > 0) {
                return goToFailScripts[Random.Shared.Next(goToFailScripts.Count)];
            }
        }

        if (Index >= State.Contents.Length - 1) {
            return null;
        }

        return State;
    }

    private void PerformJobScript() {
        if (State?.Type != ScriptStateType.Job) {
            return;
        }

        if (JobCondition == null) {
            return;
        }

        if (JobCondition.Mesos > 0) {
            if (session.Currency.CanAddMeso(-JobCondition.Mesos) != -JobCondition.Mesos) {
                session.Send(ChatPacket.Alert(StringCode.s_err_lack_meso));
                return;
            }
            session.Currency.Meso -= JobCondition.Mesos;
        }

        if (JobCondition.MoveMapId > 0) {
            session.Send(session.PrepareField(JobCondition.MoveMapId, portalId: JobCondition.MovePortalId > 0 ? JobCondition.MovePortalId : -1)
                ? FieldEnterPacket.Request(session.Player)
                : FieldEnterPacket.Error(MigrationError.s_move_err_default));
        }
    }

    private ScriptState? GetQuestScriptState(ScriptMetadata? scriptMetadata) {
        if (scriptMetadata == null) {
            return null;
        }
        session.Quest.TryGetQuest(scriptMetadata.Id, out Quest? quest);
        QuestState questState = quest?.State ?? QuestState.None;

        int stateId = questState switch {
            QuestState.None => NpcTalkUtil.GetFirstStateScript(scriptMetadata.States.Keys.ToArray(), 100, 200),
            QuestState.Started => NpcTalkUtil.GetFirstStateScript(scriptMetadata.States.Keys.ToArray(), 200, 300),
            _ => 0,
        };

        if (quest != null && session.Quest.CanStart(quest.Metadata)) {
            stateId = NpcTalkUtil.GetFirstStateScript(scriptMetadata.States.Keys.ToArray(), 200, 300);
        }

        if (quest != null && quest.Metadata.Basic.CompleteNpc == Npc?.Value.Id) {
            stateId = NpcTalkUtil.GetFirstStateScript(scriptMetadata.States.Keys.ToArray(), 300, 400);
        }

        return scriptMetadata.States.TryGetValue(stateId, out ScriptState? scriptState) ? scriptState : null;
    }

    private NpcTalkButton GetButton() {
        if (State == null) {
            return NpcTalkButton.None;
        }

        CinematicContent? content = State.Contents.ElementAtOrDefault(Index);
        if (content == null) {
            return NpcTalkButton.None;
        }
        if (content.ButtonType != NpcTalkButton.None) {
            return content.ButtonType;
        }

        if (content.Distractors.Length > 0) {
            return NpcTalkButton.SelectableDistractor;
        }

        if (Index < State.Contents.Length - 1) {
            return NpcTalkButton.Next;
        }

        if (TalkType.HasFlag(NpcTalkType.Select)) {
            if (State.Contents.Length > 0) {
                return NpcTalkButton.SelectableTalk;
            }
            return NpcTalkButton.None;
        }

        switch (State.Type) {
            case ScriptStateType.Job:
                switch (Npc?.Value.Metadata.Basic.Kind) {
                    case >= 30 and < 40: // Beauty
                        return NpcTalkButton.SelectableBeauty;
                    case 80:
                        return NpcTalkButton.ChangeJob;
                    case 81:
                        return NpcTalkButton.PenaltyResolve;
                    case 82:
                        return NpcTalkButton.TakeBoat;
                    case 501:
                        return NpcTalkButton.Roulette;
                }
                break;
            case ScriptStateType.Select:
                switch (Npc?.Value.Metadata.Basic.Kind) {
                    case 1 or > 10 and < 20: // Shop
                        return NpcTalkButton.None;
                    case 2: // Storage
                        return NpcTalkButton.None;
                }
                return NpcTalkButton.SelectableTalk;
            case ScriptStateType.Quest:
                switch (State.Id / 100) {
                    case 1:
                        return NpcTalkButton.QuestAccept;
                    case 2:
                        return NpcTalkButton.QuestProgress;
                    case 3:
                        return NpcTalkButton.QuestComplete;
                }
                break;
        }

        return NpcTalkButton.Close;
    }

    public void ProcessScriptFunction(bool enter = true) {
        if (State == null || session.Field is null) {
            return;
        }

        if (!session.ServerTableMetadata.ScriptFunctionTable.Entries.TryGetValue(metadata!.Id, out Dictionary<int, Dictionary<int, ScriptFunctionMetadata>>? scriptFunctions) ||
            !scriptFunctions.TryGetValue(State.Id, out Dictionary<int, ScriptFunctionMetadata>? functions) ||
            !functions.TryGetValue(State.Contents.ElementAt(Index).FunctionId, out ScriptFunctionMetadata? scriptFunction)) {
            return;
        }

        if ((scriptFunction.EndFunction & enter) ||
            (!scriptFunction.EndFunction & !enter)) {
            return;
        }

        // NpcTalk packets have to be sent first before any movement, items, etc
        if (scriptFunction.PortalId > 0) {
            session.Send(NpcTalkPacket.MovePlayer(scriptFunction.PortalId));
        }

        if (!string.IsNullOrEmpty(scriptFunction.UiName)) {
            session.Send(NpcTalkPacket.OpenDialog(scriptFunction.UiName, scriptFunction.UiArg));
            SendMaidCraftQueue(scriptFunction.UiName);
        }

        if (!string.IsNullOrEmpty(scriptFunction.MoveMapMovie)) {
            session.Send(NpcTalkPacket.Cutscene(scriptFunction.MoveMapMovie, scriptFunction.MoveMapId));
        }

        if (scriptFunction.PortalId > 0 && session.Field.TryGetPortal(scriptFunction.PortalId, out FieldPortal? dstPortal)) {
            session.SendMoveByPortal(PortalPacket.MoveByPortal(session.Player, dstPortal));
        }

        if (scriptFunction.CollectItems.Count > 0) {
            if (!session.Item.Inventory.ConsumeItemComponents(scriptFunction.CollectItems.ToList())) {
                logger.Error("Failed to consume items for script function {FunctionId} on npc {NpcId}", scriptFunction.FunctionId, Npc?.Value.Id);
            }
        }

        if (!string.IsNullOrEmpty(scriptFunction.SetTriggerValueKey)) {
            if (int.TryParse(scriptFunction.SetTriggerValue, out int value)) {
                session.Field.UserValues[scriptFunction.SetTriggerValueKey] = value;
            }
        }

        if (scriptFunction.CollectMeso > 0) {
            session.Currency.Meso -= scriptFunction.CollectMeso;
        }

        IList<Item> items = new List<Item>();
        foreach (ItemComponent item in scriptFunction.PresentItems) {
            Item? newItem = session.Field?.ItemDrop.CreateItem(item.ItemId, item.Rarity, item.Amount);
            if (newItem == null) {
                continue;
            }
            session.Item.Inventory.Add(newItem, true);
            items.Add(newItem);
        }

        if (items.Count > 0) {
            session.Send(NpcTalkPacket.RewardItem(items));
        }

        if (scriptFunction.PresentExp > 0) {
            session.Player.Value.Character.Exp += scriptFunction.PresentExp;
            session.Send(NpcTalkPacket.RewardExp(scriptFunction.PresentExp));
        }

        if (scriptFunction.MoveMapId > 0) {
            session.Send(session.PrepareField(scriptFunction.MoveMapId, portalId: scriptFunction.MovePortalId > 0 ? scriptFunction.MovePortalId : -1)
                ? FieldEnterPacket.Request(session.Player)
                : FieldEnterPacket.Error(MigrationError.s_move_err_default));
        }

        ApplyMaidScriptFunction(scriptFunction);
    }

    /// <summary>
    /// The craft window draws from the queue the client was last sent, and it forgets that when
    /// it closes. Hand the maid's queue over as the window opens, or a craft already running
    /// shows nothing and the player is told the maid is busy when they try to order again.
    /// </summary>
    private void SendMaidCraftQueue(string uiName) {
        if (!string.Equals(uiName, "OpenManufactureDialog", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        if (Npc?.Maid?.Craft is not MaidCraftItem craft) {
            session.Send(MaidPacket.LoadCraft([]));
            return;
        }

        // Remaining was only ever set when the craft was ordered.
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        craft.Remaining = (int) Math.Max(0, craft.StartTime + craft.LeadTime - now);

        session.Send(MaidPacket.LoadCraft([craft]));
    }

    /// <summary>
    /// Maid dialogue moves the maid's closeness and mood; each line carries how much through
    /// its script function. Lines the maid dislikes carry a negative mood change.
    /// </summary>
    private void ApplyMaidScriptFunction(ScriptFunctionMetadata scriptFunction) {
        if (Npc?.Maid is not Maid maid) {
            return;
        }

        if (scriptFunction.MaidClosenessIncrease == 0 && scriptFunction.MaidMoodIncrease == 0 && !scriptFunction.MaidPay) {
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // Salary is charged first: a line that cannot be paid for should not hand out the
        // closeness and mood that come with paying.
        if (scriptFunction.MaidPay && !PayMaidSalary(maid, now)) {
            return;
        }

        MaidUtil.ApplyMoodDecay(session, maid, now);
        MaidUtil.AddCloseness(session, maid, scriptFunction.MaidClosenessIncrease, now);
        MaidUtil.AddMood(maid, scriptFunction.MaidMoodIncrease, now);
        MaidUtil.Refresh(session, maid);

        logger.Debug("[Maid] script {ScriptId} closeness {Closeness:+#;-#;0} mood {Mood:+#;-#;0} -> grade {Level} exp {Exp} mood {MoodValue}",
            scriptFunction.ScriptId, scriptFunction.MaidClosenessIncrease, scriptFunction.MaidMoodIncrease,
            maid.ClosenessLevel, maid.ClosenessExp, maid.Mood);
    }

    /// <summary>
    /// Pays the maid's salary. MaidSalaryTable prices it in merets or mesos, and a payment
    /// restarts the MaidReadyToPay countdown the dialogue branches on.
    /// </summary>
    private bool PayMaidSalary(Maid maid, long now) {
        if (!session.TableMetadata.MaidSalaryTable.Entries.TryGetValue(maid.MaidId, out MaidSalaryTable.Entry? salary) || salary.Amount <= 0) {
            logger.Warning("No salary for maid {MaidId}", maid.MaidId);
            return false;
        }

        switch ((MaidSalaryType) salary.SalaryType) {
            case MaidSalaryType.Meret:
                if (session.Currency.Meret < salary.Amount) {
                    logger.Debug("Maid {MaidId} salary needs {Amount} merets", maid.MaidId, salary.Amount);
                    return false;
                }

                session.Currency.Meret -= salary.Amount;
                break;
            default:
                if (session.Currency.Meso < salary.Amount) {
                    logger.Debug("Maid {MaidId} salary needs {Amount} mesos", maid.MaidId, salary.Amount);
                    return false;
                }

                session.Currency.Meso -= salary.Amount;
                break;
        }

        // Paying renews the contract for another employment period. Script 8010 pays an
        // already expired maid, so the new period starts at the payment rather than at an
        // expiry date that has gone by. The period lives on the contract item, which is both
        // what the housing tool shows and what the maid is reloaded from, so it moves there.
        long employment = session.ServerTableMetadata.ConstantsTable.PeriodOfMaidEmployment * 24L * 60 * 60;
        maid.ExpiryTime = Math.Max(now, maid.ExpiryTime) + employment;
        maid.PayTime = now;

        Item? contract = maid.ContractItemUid > 0 ? session.Item.Furnishing.GetCube(maid.ContractItemUid) : null;
        if (contract is null) {
            logger.Warning("Maid {MaidId} has no contract item to renew", maid.MaidId);
        } else {
            contract.ExpiryTime = maid.ExpiryTime;
            using GameStorage.Request db = session.GameStorage.Context();
            db.SaveItems(session.AccountId, contract);
        }

        logger.Information("[Maid] paid {Amount} ({SalaryType}) to maid {MaidId}, contract now ends {Expiry}",
            salary.Amount, (MaidSalaryType) salary.SalaryType, maid.MaidId, DateTimeOffset.FromUnixTimeSeconds(maid.ExpiryTime).LocalDateTime);

        return true;
    }

    #region EnchantTalk
    public void EnchantResultScript(ScriptEventType eventType, EnchantResult result = EnchantResult.None, ItemEnchantError error = ItemEnchantError.s_itemenchant_unknown_err, int enchantLevel = 0, short rarity = 0, int failCount = 0) {
        int eventId = 0;

        if (!session.ServerTableMetadata.ScriptEventConditionTable.Entries.TryGetValue(eventType, out Dictionary<int, ScriptEventConditionMetadata>? scriptDict)) {
            return;
        }

        foreach ((int id, ScriptEventConditionMetadata value) in scriptDict) {
            if (EventConditionCheck(value, enchantLevel, rarity, failCount, result, error)) {
                eventId = id;
                break;
            }
        }

        RunEventScript(eventId);
    }

    private bool EventConditionCheck(ScriptEventConditionMetadata scriptMetadata, int enchantLevel, short rarity, int failCount, EnchantResult result, ItemEnchantError error) {
        if (error != ItemEnchantError.s_itemenchant_unknown_err && scriptMetadata.ErrorCode != error) {
            return false;
        }

        if (scriptMetadata.Rarity > 0 && scriptMetadata.Rarity != rarity) {
            return false;
        }

        if (!scriptMetadata.EnchantLevel.Contains(enchantLevel)) {
            return false;
        }

        if (scriptMetadata.FailCount > 0 || scriptMetadata.DamageType != EnchantDamageType.None) {
            // Returning false here because we don't have the fail count or damage type
            return false;
        }

        if (scriptMetadata.ResultType != EnchantResult.None && scriptMetadata.ResultType != result) {
            return false;
        }

        return true;
    }

    private void RunEventScript(int eventId) {
        if (eventId == 0) {
            return;
        }
        CinematicEventScript? script = eventScripts.FirstOrDefault(eventScript => eventScript.Id == eventId);
        if (script == null) {
            return;
        }
        ScriptContent content = script.Contents[Random.Shared.Next(script.Contents.Length)];
        session.Send(NpcTalkPacket.Update(content));
    }
    #endregion
}
