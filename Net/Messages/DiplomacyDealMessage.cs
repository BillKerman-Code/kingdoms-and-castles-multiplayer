using Riptide;

namespace KaCMultiplayer.Net.Messages
{
    /// <summary>What one kingdom is saying to another about money and peace.</summary>
    public enum DealKind
    {
        /// <summary>"Pay me this and I will stop." The recipient is the one who pays.</summary>
        Demand = 0,

        /// <summary>"Take this and stop." The sender is the one who pays.</summary>
        Offer = 1,

        /// <summary>
        /// The recipient agrees. Gold moves and the war ends immediately, in that order; anything
        /// else waits one extra hop for <see cref="Resolved"/> before it can be reported, see that
        /// entry for why.
        /// </summary>
        Accept = 2,

        /// <summary>The recipient declines. Nothing moves and the deal is forgotten.</summary>
        Refuse = 3,

        /// <summary>
        /// Sent only by the PAYER's own machine, after Accept, once it has actually taken the
        /// goods out of its own real storage. FromTeam is the payer, ToTeam the payee, Amount what
        /// was actually taken, Resource unchanged from the original deal.
        ///
        /// EXISTS BECAUSE ACCEPT USED TO RESOLVE EVERYTHING BY ITSELF, computing the transfer
        /// identically on every machine on the assumption the outcome was the same everywhere --
        /// true for gold (see PlayerRelations.MoveResource), false for anything stored in a
        /// job-worked building. A kingdom's own production jobs are only ever assigned by that
        /// kingdom's own machine (see Main.cs's JobUpdateAssignmentForeignHook, gating
        /// Job.UpdateAssignment), and nothing ever broadcast the result, so every OTHER machine's
        /// copy of that kingdom's storage can be, and in practice is, badly wrong. Before this
        /// existed, that meant a demand for 10 Wood really did remove 10 Wood from the payer
        /// (whose own machine read its own real storage correctly) while the payee's own machine,
        /// independently reasoning out the SAME transfer from its foreign copy of that storage,
        /// computed 0 available and deposited 0 -- "0 Wood paid" was not a display bug, the wood
        /// was genuinely destroyed. Resolved lets the one machine that can trust its own reading
        /// decide, and everyone else, including the payee's own machine for the deposit half,
        /// waits for that number instead of computing their own.
        /// </summary>
        Resolved = 4,

        /// <summary>
        /// Sent only by the PAYEE's own machine, after Resolved, and only when it could not fit
        /// everything (a full warehouse, most likely). FromTeam is the payee sending it back,
        /// ToTeam the payer who deposits it again, for real, on their own machine. Mirrors the
        /// "clamped rather than destroyed" rule this whole handshake exists to keep, now that
        /// taking and giving can no longer happen as one step on one machine.
        /// </summary>
        Returned = 5,
    }

    /// <summary>
    /// A payment proposed between two kingdoms, and the answer to one.
    ///
    /// Covers tribute and peace with the same message because they are the same transaction seen
    /// from different ends: somebody pays, and if there is a war it stops. An attacker naming a
    /// price and a defender offering one differ only in who pays, which is what
    /// <see cref="DealKind"/> records.
    ///
    /// Both teams travel explicitly, as with <see cref="PlayerRelationMessage"/>, so a receiver
    /// never has to work out who the other side was. Demand, Offer, Accept and Refuse are relayed
    /// to everyone including the sender and reasoned out identically everywhere, so nobody has to
    /// be told separately what was proposed or decided.
    ///
    /// NOT GOLD ONLY, any more. <see cref="KaCMultiplayer.Lobby.ResourcePicker"/> lets a player
    /// choose any of <c>PlayerRelations.Demandable</c> for the Resource field below, and Resource
    /// has always been carried as a generic int for exactly that reason -- this comment used to
    /// say "GOLD ONLY. Goods would need a resource picker in a UI that does not exist yet", which
    /// stopped being true the moment ResourcePicker was built and was never corrected.
    ///
    /// "Reasoned out identically everywhere" stops being true, though, the instant the resource is
    /// not gold: see <see cref="DealKind.Resolved"/> for why moving anything else needs the two
    /// extra message kinds below and cannot be settled by Accept alone. See
    /// PlayerRelations.ApplyDeal for what accepting actually does.
    /// </summary>
    public class DiplomacyDealMessage : IOriginated
    {
        public NetMessageId Id { get { return NetMessageId.DiplomacyDeal; } }

        public ushort Origin { get; set; }

        /// <summary>The team proposing, or the team answering.</summary>
        public int FromTeam;

        /// <summary>The team being proposed to, or whose proposal is being answered.</summary>
        public int ToTeam;

        public int Kind;

        /// <summary>How much of it. Ignored on Accept and Refuse, which answer the open deal.</summary>
        public int Amount;

        /// <summary>
        /// Which resource, as a <c>FreeResourceType</c>. Gold is simply one of them (value 4).
        ///
        /// Carried as an int rather than the enum so the wire format does not move if the game
        /// ever inserts a type: the receiver validates it against the live enum before use, and a
        /// value it does not recognise is dropped rather than cast blindly into something real.
        /// </summary>
        public int Resource;

        public void Serialize(Message m)
        {
            m.AddUShort(Origin);
            m.AddInt(FromTeam);
            m.AddInt(ToTeam);
            m.AddInt(Kind);
            m.AddInt(Amount);
            m.AddInt(Resource);
        }

        public void Deserialize(Message m)
        {
            Origin = m.GetUShort();
            FromTeam = m.GetInt();
            ToTeam = m.GetInt();
            Kind = m.GetInt();
            Amount = m.GetInt();
            Resource = m.GetInt();
        }
    }
}
