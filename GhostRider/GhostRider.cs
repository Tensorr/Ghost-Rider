using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcheBuddy.Bot.Classes;

namespace GhostRider
{
    public class LazyRider : Core
    {
        #region Std plugin settings

        public static string GetPluginAuthor()
        {
            return "Rostol";
        }

        public static string GetPluginVersion()
        {
            return "0.3.0";
        }

        public static string GetPluginDescription()
        {
            return
                "Lazy Rider Plugin, Optimized for Sorc - Occ - Witch but modifiable for all changing DoRotation, thanks @OUT";
        }

        #endregion

        private readonly List<uint> _itemsToUnseal = new List<uint>
        {
            32458, 32463, 32462, 32464, 33116, 33117, 33307, 33310,
            33493, 33496, 33609, 33612, 33777, 33780 };

        private List<uint> _itemsToDelete = new List<uint>
        {
            15694, 29498, 19563, 17801, 19960, 14482, 17825,
            15822, 14830, 23700, 26106, 16006, 21131, 24422 };

        private List<uint> _itemsToDisenchant = new List<uint>
        {
            33440, 33350, 33361, 33360, 33362, 33374, 33370,
            33369, 33351, 33371, 33466, 33468, 33467, 33439,
            22132, 22763 };

        private List<uint> _itemsToSell = new List<uint>
        {
            32110, 32134, 32102, 32123, 32099, 23387, 6127, 23390, 6152, 23388,
            6077, 32166, 32113, 8012, 7992, 32145, 32152, 32170, 32175, 33000,
            32169, 32176, 32173, 22103, 6202, 33213, 33005, 22159, 32184, 22230,
            33226, 32996, 33224, 33361, 32984, 33233, 32981, 33234, 32978, 33225,
            32990, 32987, 33235 };

        private List<uint> _mpFoodList = new List<uint>
        {

        };

        private List<uint> _mpPotsList = new List<uint>
        {
            18792, 34008, 34009, 31770
        };

        private List<uint> _hpFoodList = new List<uint>
        {
            34003, 34001, 34000, 17664
        };

        private List<uint> _hpPotsList = new List<uint>
        {
            18791, 34006, 34007, 15580
        };

        private List<uint> _coinpursesList = new List<uint>
        {
            29203, 29204, 29205, 29206, 29207
        };

        private int _dist = 40;

        public bool LootingEnabled
        {
            get { return true; }
        }

        public void PluginRun()
        {
            new Task(CancelAttacksOnAnothersMobs).Start();
            new Task(Watchdog).Start();

            SetGroupStatus("GhostRider", false);//Is this thing on or what ?
            SetGroupStatus("Inventory", false); //Open Purses
            SetGroupStatus("Farm", false);      //Do i keep on killing mobs ?
            SetGroupStatus("AFK", false);       //If AFK is enabled some timings are made very relaxed (ie open a purse a minute or so) 
            SetGroupStatus("Eat/Drink", false); //Eat after fighting, take potions during fight, drink bevs as buff
            SetGroupStatus("Loot", true);       //Loot corpses? this was re-added as sometimes there are problems looting.
            while (true)
            {
                try
                {
                    if (!me.isAlive() && GetGroupStatus("GhostRider"))
                        DoResurrect();

                    if (!GetGroupStatus("GhostRider") || !me.isAlive())
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                }
                catch {}
                Thread.Sleep(50);
                //If GhostRider checkbox enabled in widget and our character alive
                //am i under attack (better way?) Or do i have someone targetted
                try
                {
                    if ((GetGroupStatus("Farm") ? getAggroMobs().Count > 0 : me.inFight) ||
                        (me.target != null && isAttackable(me.target) && isAlive(me.target)))
                    {
                        try
                        {
                            if (me.target == null || getAggroMobs(me).Count > 0)
                                SetTarget(getAggroMobs(me).First());
                            // dont have a target selected, attack the one attacking me.
                            if (me.target == null && GetGroupStatus("Farm"))
                                SetTarget(getAggroMobs().First());
                        }
                        catch
                        {
                            if (getAggroMobs(me).Count > 0) SetTarget(getAggroMobs(me).First());
                            continue;
                        }

                        if (me.target != null && (me.angle(me.target) > 45 && me.angle(me.target) < 315))
                            TurnDirectly(me.target); //Face Target if not facing it.

                        if (me.target != null && isAlive(me.target))
                            DoRotation(me.target);

                        MySleep(100, 333);
                    }

                    //if (!me.isAlive()) continue;
                    try
                    {
                        if (me.target != null && !me.target.isAlive())
                            LootMob(me.target);
                    }
                    catch {}
                    foreach (Creature m in getCreatures().Where(m => m.dropAvailable && me.dist(m) < 10))
                    {
                        LootMob(m);
                        MySleep(100, 333);
                    }
                    CheckBuffs();
                    UseRegenItems();
                    if (me.hpp > 66 && me.mpp > 50)
                        SearchForOtherTarget(me.target);
                    if (GetGroupStatus("Inventory")) Processinventory();
                }
                catch (Exception e)
                {
                    //PlaySound("targeting.wav");
                    Log("!!## MAIN LOOP", "GhRider");
                    LogEx(e);
                }
            }
        }

        private void DoRotation(Creature targeCreature)
        {
            while (GetGroupStatus("GhostRider"))
            {
                try
                {
                    if (!me.isAlive()) return; // if i am dead just return, its pointless to do anything else.

                    if (getAggroMobs(getMount()).Count > 0)
                        targeCreature = getAggroMobs(getMount()).First();
                            //TODO: if our mount is under attack defend it.  Unsummon it ? toggle?

                    if (!targeCreature.isAlive() || !isAttackable(targeCreature))
                    {
                        if (getAggroMobs(me).Count == 0 && getAggroMobs(getMount()).Count == 0)
                            //my target is dead and no one is attacking me or mount => return
                            return;
                        if (getAggroMobs(getMount()).Count > 0) //otherwise get my mount attacker or my attacker
                            targeCreature = getAggroMobs(getMount()).First();
                        if (getAggroMobs(me).Count > 0)
                            targeCreature = getAggroMobs(me).First();
                    }


                    int a = 0;
                    while (!SetTarget(targeCreature) && GetGroupStatus("GhostRider"))
                    {
                        Thread.Sleep(50);
                        a++;
                        if (a < 20) continue;
                        PlaySound("enemy.wav");
                        SetGroupStatus("GhostRider", false);
                            //after 20 loops sound an alarm, exit the loop && stop the plugin
                    }

                    // one of these might throw an ex but the result is the same - retrun  

                    // Be careful with spelling
                    // SKILLS NEED TO BE ORDERED BY IMPORTANCE
                    // IE: 1st : Heal cond: me.hp <33f
                    // use: UseSkillIf if you need a  

                    //HEAL
                    if (UseSkillIf("Enervate", (me.hpp < 50)))
                    {
                        Log("Enervate " + me.hpp, "GhRider");
                        MySleep(100, 300);
                        if (UseSkillIf("Earthen Grip", (me.hpp < 50)))
                        {
                            Log("Earthen " + me.hpp, "GhRider");
                        }
                        continue;
                    }
                    UseRegenItems();
                    UseSkillIf("Absorb Lifeforce", (me.hpp < 66));

                    //CLEAN DEBUF


                    //FIGHT
                    //if (angle(me.target, me) > 45 && angle(me.target, me) < 315)


                    if (getAggroMobs(me).Count > 1 && TargetsWithin(8) > 1 && me.hpp < 75) //AOE First id i am not 100%
                    {
                        if (UseSkillIf("Summon Crows",
                            UseSkillIf("Hell Spear", skillCooldown("Summon Crows") == 0L)))
                            continue;
                        if (UseSkillIf("Searing Rain",
                            UseSkillIf("Freezing Earth", skillCooldown("Searing Rain") == 0L)))
                            continue;
                    }

                    if (UseSkillIf("Hell Spear",
                        (((targeCreature.hpp >= 33) && (me.hpp < 66)) || (getAggroMobs(me).Count > 1)) &&
                        (me.dist(me.target) <= 8) //only if in range
                        ))
                    {
                        UseSkillIf("Summon Crows", (getAggroMobs(me).Count > 1) || (me.hpp < 50));
                        UseSkillIf("Arc Lightning", (getAggroMobs(me).Count == 1));
                    }

                    UseSkillIf("Mana Force", me.hpp < 75 && me.dist(me.target) <= 5);
                    UseSkillIf("Freezing Arrow",targeCreature.hpp >= 33);
                    UseSkillIf("Insidious Whisper", me.dist(me.target) <= 8);

                    UseSkillIf("Freezing Earth",
                        (((targeCreature.hpp >= 33) && (me.hpp < 66)) || (getAggroMobs(me).Count > 1)) &&
                        (me.dist(me.target) <= 8)); //only if in range

                    //Do FA-Fb-Fb-Fb OR Fb-Fb-Fb if FA is inCoolDown Try to combo with ^ skill
                    UseSkillIf("Flamebolt",
                        UseSkillIf("Flamebolt",
                            UseSkillIf("Flamebolt",
                                (skillCooldown("Freezing Arrow") > 0L 
                                 || targeCreature.hpp < 33 
                                 || UseSkillIf("Freezing Arrow", targeCreature.hpp >= 33)))));

                    //BUFF     //While fighting??  
                    UseSkillIf("Insulating Lens", (buffTime("Insulating Lens (Rank 3)") == 0 && me.hpp < 66));
                }
                catch (Exception ex)
                {
                    PlaySound("targeting.wav");
                    Log("!!## ROTATIONS", "GhRider");
                    LogEx(ex);
                }
            }
        }

        //Try to find best mob in farm zone.
        public Creature GetBestNearestMob(Creature target, double dist = 0)
        {
            if (dist == 0) dist = _dist;
            //Creature mob;
            return getCreatures().AsParallel().ToArray()
                .Where(obj =>
                    obj.type == BotTypes.Npc 
                    && isAttackable(obj) && //(obj.level - me.level) < 4 && 
                    (obj.firstHitter == null || obj.firstHitter == me || obj.aggroTarget == me)
                    && isAlive(obj)
                    && me.dist(obj) < dist)
                .OrderBy(obj => me.dist(obj))
                .FirstOrDefault(mob => mob != null);
        }

        /// <summary>
        ///     Cancel skill if mob which we want to kill already attacked by another player.
        ///     not working ...
        ///     It runs as an independant thread checking GR status every 200ms
        ///     function never returns - BY DESIGN this is an independant infinte thread.
        /// </summary>
        public void CancelAttacksOnAnothersMobs()
        {
            while (true)
            {
                while (GetGroupStatus("GhostRider") && GetGroupStatus("Farm"))
                {
                    try
                    {
                        if (me.isPartyMember) continue;
                        if (me.target != null && me.target.firstHitter != null && me.target.firstHitter != me)
                        {
                            if (me.isCasting) CancelSkill();
                            var oldtarget = me.target;
                            
                            CancelTarget();
                            SetTarget(
                                getCreatures().AsParallel().ToArray()
                                    .Where(obj =>
                                        obj != oldtarget &&
                                        obj.type == BotTypes.Npc
                                        && isAttackable(obj) && //(obj.level - me.level) < 4 && 
                                        (obj.firstHitter == null || obj.firstHitter == me || obj.aggroTarget == me)
                                        && isAlive(obj)
                                        && me.dist(obj) < _dist)
                                    .OrderBy(obj => me.dist(obj))
                                    .FirstOrDefault(mob => mob != null)
                                );
                        }
                         

                        Thread.Sleep(200);
                    }
                    catch (Exception ex)
                    {
                        Log("!!## CancelAttacksOnAnotherMobs", "GhRider");
                        LogEx(ex);
                    }

                }
                Thread.Sleep(200);
            }
        }

        /// <summary>
        ///     This is an overseer, if i disable the main rider function STOP everything.
        ///     It runs as an independant thread checking GR status every 200ms
        ///     function never returns - BY DESIGN this is an independant infinte thread.
        /// </summary>
        public void Watchdog()
        {
            while (true) // this one has to be endless
            {
                while (GetGroupStatus("GhostRider"))
                {
                    Thread.Sleep(200); // 5 times a sec should be enough
                }
                //CancelSkill();
                //CancelTarget();
                StopAllmove();

                Thread.Sleep(200);
            }
        }


        //Check our buffs
        public void CheckBuffs()
        {
            if (buffTime("Insulating Lens (Rank 3)") == 0 && skillCooldown("Insulating Lens") == 0)
            {
                SuspendMoveToBeforeUseSkill(true);
                UseSkillAndWait("Insulating Lens", true);
                SuspendMoveToBeforeUseSkill(false);
            }

            if (buffTime("Fruit Liquor") == 0 && GetGroupStatus("Eat/Drink"))
            {
                UseItemAndWait(17668);
            }
            if (buffTime("Fruit Punch") == 0 && GetGroupStatus("Eat/Drink"))
            {
                UseItemAndWait(8501);
            }
        }

        public int CheckDebuffs(Player player)
        {
            return 0;
        }

        public bool UseSkillAndWait(string skillName, bool selfTarget = false)
        {
            //wait cooldowns first, before we try to cast skill
            while ((me.isCasting || me.isGlobalCooldown) && GetGroupStatus("GhostRider"))
                MySleep(50, 100);
            do
            {
                if (me.target != null) TurnDirectly(me.target); //face target before casting
                if (UseSkill(skillName, true, selfTarget))
                {
                    //wait cooldown again, after we start cast skill
                    while (me.isCasting && GetGroupStatus("GhostRider"))
                        Thread.Sleep(50);
                    while (me.isGlobalCooldown && GetGroupStatus("GhostRider"))
                        Thread.Sleep(10);
                    //Thread.Sleep(50); 
                    return true;
                }
            } while (GetLastError() == LastError.AlreadyCasting && GetGroupStatus("GhostRider")); // TST

            if (me.target == null || GetLastError() != LastError.NoLineOfSight) return false;
            //No line of sight, try come to target.
            ComeTo(me.target, 10);
            return false;
        }

        /// <summary>
        ///     Checks if the conditions are met to cast a skill and passes the cast call on.
        ///     IF cond evaluates to true the skill is cast
        /// </summary>
        /// <param name="skillName">Nmae of Skill to Cast - Beware with the Spelling</param>
        /// <param name="cond">Cast only if this condition evaluates to true</param>
        /// <param name="selfTarget">Target myself ? T/F</param>
        public bool UseSkillIf(string skillName, bool cond = true, bool selfTarget = false)
        {
            if (!cond || skillCooldown(skillName) != 0L || !GetGroupStatus("GhostRider")) return false;
            try
            {
                return (UseSkillAndWait(skillName, selfTarget));
            }
            catch (Exception ex)
            {
                Log("!!## UseSkillIf", "GhRider");
                LogEx(ex);
            }
            return false;
        }

        /// <summary>
        ///     Executes a simple rotation on the recieved target.
        ///     - Needs a valid target
        /// </summary>
        /// <param name="deadCreature">Target to destroy (hopefully)</param>
        public void LootMob(Creature deadCreature)
        {
            if (!GetGroupStatus("Loot")) return;
            while (deadCreature != null && !isAlive(deadCreature) && isExists(deadCreature) &&
                   deadCreature.type == BotTypes.Npc &&
                   deadCreature.dropAvailable && isAlive())
            {
                if (me.dist(deadCreature) > 3)
                    ComeTo(deadCreature, 3);
                PickupAllDrop(deadCreature);
            }
        }

        public void PluginStop()
        {
            DelGroupStatus("GhostRider"); //Is this thing on or what ?
            DelGroupStatus("Inventory"); //Open Purses
            DelGroupStatus("Farm"); //Do i keep on killing mobs ?
            DelGroupStatus("AFK");
                //If AFK is enabled some timings are made very relaxed (ie open a purse a minute or so) 
            DelGroupStatus("Eat/Drink");
        }

/*
        private void DoAFK()
        {
            Processinventory();
        }
*/

        private void DoResurrect()
        {
            StopAllmove();
            CancelTarget();
            Log("Dead: " + DateTime.Now, "GhRider");
            SetGroupStatus("Farm", false);
            
            MySleep(1000, 2000);

            while (!ResToRespoint() && GetGroupStatus("GhostRider"))
                Thread.Sleep(1000);
            while (!SetTarget("Glorious Nui") && GetGroupStatus("GhostRider"))
                MySleep(1500, 2500);

            if (me.target != null) ComeTo(me.target, 5);

            RestoreExp();
            while ((me.isGlobalCooldown || me.isCasting))
                MySleep(100, 120);
        }

        private void MySleep(int frm, int to)
        {
            var rnd = new Random();
            int wait = rnd.Next(frm, to);
            Thread.Sleep(wait);
        }

        private void StopAllmove()
        {
            MoveTo(me.X, me.Y, me.Z);
            MoveBackward(false);
            MoveForward(false);
            MoveLeft(false);
            MoveRight(false);
            Jump(false);
        }

        public int TargetsWithin(double dist)
        {
            return getCreatures().AsParallel().ToArray().Count(obj => obj.type == BotTypes.Npc &&
                                                                      isAttackable(obj) &&
                                                                      isAlive(obj) &&
                                                                      me.dist(obj) < dist);
        }

        private void SearchForOtherTarget(Creature target, bool keepattacking = false)
        {
            if (!GetGroupStatus("Farm")) return;
            try
            {
                SetTarget(GetBestNearestMob(target, 40));
            }
            catch (Exception ex)
            {
                Log("!!## SearchForOtherTarget", "GhRider");
                LogEx(ex);
            }
        }

        public void Processinventory()
        {
            foreach (
                Item i in
                    me.getItems()
                        .Where(
                            i =>
                                i.place == ItemPlace.Bag && _coinpursesList.Contains(i.id)))
                                //(i.id == 29203 || i.id == 29204 || i.id == 29205 || i.id == 29206 || i.id == 29207)))
            {
                try
                {
                    if (i == null) continue;
                    while (i.count > 0 && me.opPoints > 150 && me.isAlive() && (GetGroupStatus("Inventory")))
                    {
                        Thread.Sleep(150);
                        i.UseItem();
                        if (GetGroupStatus("AFK"))
                            MySleep(10000, 120000);
                                //TODO: This will make the bot unresponsive for that time, a better way is needed.
                        MySleep(50, 150); // it's small enough so i dont care about conditionals
                        if (!GetGroupStatus("GhostRider")) continue;

                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log("!!## Processinventory", "GhRider");
                    LogEx(ex);
                }
            }
        }


        private void UseRegenItems()
        {
            if (!me.isAlive())
                return;
            if (me.inFight)
            {
                if (me.hpp < 50)
                {
                    List<Item> itemsToUse =
                        me.getItems()
                            .FindAll(
                                i =>
                                    i.place == ItemPlace.Bag && _hpPotsList.Contains(i.id));
                    foreach (Item i in itemsToUse)
                        UseItemAndWait(i.id);
                }
                if (me.mpp < 50)
                {
                    List<Item> itemsToUse =
                        me.getItems()
                            .FindAll(
                                i =>
                                    i.place == ItemPlace.Bag && _mpPotsList.Contains(i.id) );
                    foreach (Item i in itemsToUse)
                        UseItemAndWait(i.id);
                }
            }
            else
            {
                if (me.hpp < 60)
                {
                    List<Item> itemsToUse =
                        me.getItems()
                            .FindAll(
                                i =>
                                    i.place == ItemPlace.Bag && _hpFoodList.Contains(i.id));
                    foreach (Item i in itemsToUse)
                        UseItemAndWait(i.id, true);
                }
                if (me.mpp < 70)
                {
                    List<Item> itemsToUse =
                        me.getItems()
                            .FindAll(
                                i =>
                                    i.place == ItemPlace.Bag &&
                                    (i.id == 34002 || i.id == 34005 || i.id == 34004 || i.id == 8505));
                    foreach (Item i in itemsToUse)
                        UseItemAndWait(i.id, true);
                }
            }
        }

        private void UseItemAndWait(uint itemId, bool suspendMovements = false)
        {
            while (me.isCasting || me.isGlobalCooldown)
                Thread.Sleep(50);
            if (suspendMovements)
                SuspendMoveToBeforeUseSkill(true);
            if (!UseItem(itemId, true))
            {
                if (me.target != null && GetLastError() == LastError.NoLineOfSight)
                {
                    Console.WriteLine("No line of sight, try come to target.");
                    if (dist(me.target) <= 5)
                        ComeTo(me.target, 2);
                    else if (dist(me.target) <= 10)
                        ComeTo(me.target, 3);
                    else if (dist(me.target) < 20)
                        ComeTo(me.target, 8);
                    else
                        ComeTo(me.target, 8);
                }
            }
            while (me.isCasting || me.isGlobalCooldown)
                Thread.Sleep(50);
            if (suspendMovements)
                SuspendMoveToBeforeUseSkill(false);
        }

        public void CheckSealedEquips()
        {
            if (isInPeaceZone()) //cant unseal in peace zone
                return;
            foreach (
                Item i in
                    me.getItems()
                        .Where(i => i.place == ItemPlace.Bag && ((i.id == 29203 || i.id == 29204 || i.id == 29205))))
            {
                while (i.count > 0 && me.opPoints > 150 && me.isAlive())
                {
                    Thread.Sleep(150);
                    i.UseItem();
                    Thread.Sleep(500);
                    while (me.isCasting || me.isGlobalCooldown)
                        Thread.Sleep(50);
                }
            }
            foreach (
                Item i in
                    me.getItems()
                        .Where(i => i.place == ItemPlace.Bag && ((i.id == 29203 || i.id == 29204 || i.id == 29205))))
            {
                if (i.categoryId != 153 && !_itemsToUnseal.Contains(i.id)) return;
                if (me.opPoints < 10)
                    return;
                while (i.count > 0 && me.isAlive())
                {
                    Thread.Sleep(500);
                    i.UseItem();
                    Thread.Sleep(1000);
                    while (me.isCasting || me.isGlobalCooldown)
                        Thread.Sleep(50);
                }
            }
        }

        private void LogEx(Exception ex)
        {
            Log("!!##  Message = " + ex.Message, "GhRider");
            Log("!!##  Source = " + ex.Source, "GhRider");
            Log("!!##  StackTrace = " + ex.StackTrace, "GhRider");
        }
    }
}