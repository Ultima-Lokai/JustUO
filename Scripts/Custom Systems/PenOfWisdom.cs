using System;
using System.Collections.Generic;

using Server.Commands;
using Server.Targeting;
using System.Reflection;

namespace Server.Items
{
    [FlipableAttribute(0x0FBF, 0x0FC0)]
    public class PenOfWisdom : Item, IUsesRemaining
    {
        private int m_UsesRemaining;
        [Constructable]
        public PenOfWisdom()
            : base(0x0FBF)
        {
            LootType = LootType.Blessed;
            Weight = 1.0;
            m_UsesRemaining = 100;
            Hue = 37;
        }

        [Constructable]
        public PenOfWisdom(int uses)
            : base(0x0FBF)
        {
            LootType = LootType.Blessed;
            Weight = 1.0;
            m_UsesRemaining = uses;
            Hue = 37;
        }

        public PenOfWisdom(Serial serial)
            : base(serial)
        {
        }

        public override int LabelNumber
        {
            get
            {
                return 1115358; //Fix to Pen of Wisdom
            }
        }
        public virtual bool ShowUsesRemaining
        {
            get
            {
                return true;
            }
            set
            {
            }
        }
        [CommandProperty(AccessLevel.GameMaster)]
        public int UsesRemaining
        {
            get
            {
                return this.m_UsesRemaining;
            }
            set
            {
                this.m_UsesRemaining = value;
                this.InvalidateProperties();
            }
        }
        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1060584, this.m_UsesRemaining.ToString()); // uses remaining: ~1_val~
        }

        public override void OnDoubleClick(Mobile from)
        {
            base.OnDoubleClick(from);

            if (this.IsChildOf(from.Backpack))
            {
                from.SendMessage("Target the Runebook you wish to copy.");
                from.Target = new CopySource(this);
            }
            else
                from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.

            if (UsesRemaining <= 0)
                Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write((int)0); // version

            writer.Write((int)this.m_UsesRemaining);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();

            this.m_UsesRemaining = reader.ReadInt();
        }

        private class CopySource : Target
        {
            private readonly PenOfWisdom m_Pen;
            public CopySource(PenOfWisdom pen)
                : base(12, false, TargetFlags.None)
            {
                this.m_Pen = pen;
            }

            protected override void OnTarget(Mobile from, object targ)
            {
                if (this.m_Pen == null || this.m_Pen.Deleted)
                    return;

                if (!(targ is Runebook))
                {
                    from.SendMessage("You can only use this on runebooks.");
                    return;
                }

                Container pack = from.Backpack;

                if (pack == null)
                {
                    from.SendMessage("Please ensure you have a pack.");
                    return;
                }

                Item source = (Item)targ;
                if (source.RootParent != from)
                {
                    from.SendMessage("The runebook you wish to copy must be in your backpack.");
                    return;
                }

                int runes = ((Runebook)targ).Entries.Count;
                if (runes < 1)
                {
                    from.SendMessage("You cannot use this on an empty runebook.");
                    return;
                }

                from.SendMessage("Target an empty Runebook.");
                from.Target = new CopyTarget(m_Pen, source);
            }
        }

        private class CopyTarget : Target
        {
            private readonly PenOfWisdom m_Pen;
            private readonly Item m_Source;

            public CopyTarget(PenOfWisdom pen, Item source)
                : base(12, false, TargetFlags.None)
            {
                m_Pen = pen;
                m_Source = source;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                Item target = ((Item)targeted);

                Container pack = from.Backpack;

                if (pack == null)
                {
                    from.SendMessage("Please ensure you have a pack.");
                    return;
                }

                if (target == null || target.Deleted || !(target is Runebook) || ((Runebook)target).Entries.Count > 0)
                {
                    from.SendMessage("You can only copy to an empty runebook.");
                    return;
                }

                if (target.RootParent != from)
                {
                    from.SendMessage("The runebook you wish to write to must be in your backpack.");
                    return;
                }

                if (ConsumeTotal(pack, ((Runebook)m_Source).Entries.Count, true) > -1)
                {
                    from.SendMessage("This operation requires unmarked recall runes and mark scrolls.");
                    from.SendMessage("Total of each needed: {0}.", ((Runebook)m_Source).Entries.Count);
                    return;
                }

                Type t = typeof(Runebook);

                ConstructorInfo c = t.GetConstructor(Type.EmptyTypes);

                if (c != null)
                {
                    try
                    {
                        from.SendMessage("Writing Copy...");

                        object o = c.Invoke(null);

                        if (o != null && o is Item)
                        {
                            Item newItem = (Item)o;
                            Dupe.CopyProperties(newItem, m_Source);
                            m_Source.OnAfterDuped(newItem);
                            newItem.Parent = null;
                            pack.DropItem(newItem);

                            newItem.InvalidateProperties();
                            from.SendMessage("Done");
                            m_Pen.UsesRemaining -= 1;
                            m_Pen.InvalidateProperties();
                            target.Delete();
                        }
                    }
                    catch
                    {
                        from.SendMessage("Error, please notify a GM!");
                    }
                }
            }
        }

        private static int ConsumeTotal(Container pack, int amount, bool recurse)
        {
            Item[] scrolls = pack.FindItemsByType(typeof(MarkScroll), recurse);
            int foundScrolls = 0;
            int foundRunes = 0;

            for (int j = 0; j < scrolls.Length; ++j)
            {
                foundScrolls += scrolls[j].Amount;
            }

            if (foundScrolls < amount)
            {
                return amount - foundScrolls;
            }

            Item[] runes = pack.FindItemsByType(typeof(RecallRune), recurse);
            List<Item> unmarkedRunes = new List<Item>();

            for (int j = 0; j < runes.Length; ++j)
            {
                if (!((RecallRune)runes[j]).Marked)
                {
                    foundRunes += runes[j].Amount;
                    unmarkedRunes.Add(runes[j]);
                }
            }

            if (foundRunes < amount)
            {
                return amount - foundRunes;
            }

            int need = amount;

            for (int j = 0; j < amount; ++j)
            {
                Item item = scrolls[j];

                int theirAmount = item.Amount;

                if (theirAmount < need)
                {
                    item.Delete();
                    need -= theirAmount;
                }
                else
                {
                    item.Consume(need);
                    break;
                }
            }

            for (int j = 0; j < amount; ++j)
            {
                unmarkedRunes[j].Delete();
            }

            return -1;
        }
    }
}