using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            return "0.2.1";
        }

        public static string GetPluginDescription()
        {
            return "Lazy Rider Plugin, Optimized for Sorc - Occ - Witch but modifiable for all changing DoRotation, thanks @OUT";
        }
        #endregion
        //Try to find best mob in farm zone.
        public Creature GetBestNearestMob(Creature target, double dist=99)
        {
            //Creature mob;
            return getCreatures().AsParallel().ToArray()
                .Where(obj => 
                    obj.type == BotTypes.Npc && isAttackable(obj) && //(obj.level - me.level) < 4 && 
                    (obj.firstHitter == null || obj.firstHitter == me) && isAlive(obj) && 
                    me.dist(obj) < dist && (hpp(obj) == 100 || obj.aggroTarget == me))
                    .OrderBy(obj => me.dist(obj))
                    .FirstOrDefault(mob => mob != null);
        }

        //Cancel skill if mob which we want to kill already attacked by another player.
        // not working ... 
        // function never returns - BY DESIGN this is an independant infinte thread.
        public void CancelAttacksOnAnothersMobs()
        {
            while (true)
            {
                if (me.isCasting && me.target != null && me.target.firstHitter != null && me.target.firstHitter != me)
                    CancelSkill();
                Thread.Sleep(100);
            }
        }

        //Check our buffs
        public void CheckBuffs()
        {

            if (buffTime("Insulating Lens (Rank 1)") == 0 && skillCooldown("Insulating Lens") == 0)
            {
                SuspendMoveToBeforeUseSkill(true);
                UseSkillAndWait("Insulating Lens", true);
                SuspendMoveToBeforeUseSkill(false);
            }
            
        }

        public bool UseSkillAndWait(string skillName, bool selfTarget = false)
        {
            //wait cooldowns first, before we try to cast skill
            while (me.isCasting || me.isGlobalCooldown)
                MySleep(100,200);
            if (UseSkill(skillName, true, selfTarget))
            {
                //wait cooldown again, after we start cast skill
                while (me.isCasting || me.isGlobalCooldown)
                    Thread.Sleep(50);
                return true;
            }
            if (me.target == null || GetLastError() != LastError.NoLineOfSight) return false;
            //No line of sight, try come to target.
            if (dist(me.target) <= 5)
                ComeTo(me.target, 2);
            else if (dist(me.target) <= 10)
                ComeTo(me.target, 3);
            else if (dist(me.target) < 20)
                ComeTo(me.target, 8);
            else
                ComeTo(me.target, 10);
            return false;
        }
        /// <summary>
        ///    Checks if the conditions are met to cast a skill and passes the cast call on.
        ///         IF cond evaluates to true the skill is cast     
        /// </summary>
        /// <param name="skillName">Nmae of Skill to Cast - Beware with the Spelling</param>
        /// <param name="cond">Cast only if this condition evaluates to true</param>
        /// <param name="selfTarget">Target myself ? T/F</param>
        public bool UseSkillIf(string skillName, bool cond = true, bool selfTarget = false)
        {
            if (cond && skillCooldown(skillName)==0L)
            {
                try
                {
                    return (UseSkillAndWait(skillName, selfTarget));
                }
                catch(Exception ex){
                    LogEx(ex);
                }
            }
            return false;
        }
        /// <summary>
        /// Executes a simple rotation on the recieved target.
        ///  - Needs a valid target 
        /// </summary>
        /// <param name="targeCreature">Target to destroy (hopefully)</param>
        private void DoRotation(Creature targeCreature)
        {
            
            var aaa = false; //trash value

            while (GetGroupStatus("GhostRider"))
            {
                // one of these might throw an ex but the result is the same - retrun  
                try
                {
                    if (!me.isAlive()) return;
                    if (me.target == null) return;
                    if (!targeCreature.isAlive() || !isAttackable(targeCreature)) return;
                    //if (me.target != targeCreature) targeCreature = me.target;
                }
                catch { return; }
                // Be careful with spelling
                // SKILLS NEED TO BE ORDERED BY IMPORTANCE
                // IE: 1st : Heal cond: me.hp <33f
                // use: UseSkillIf if you need a  

                //HEAL
                if (UseSkillIf("Enervate", (me.hpp < 50)))
                {
                    Log("Enervate " + me.hpp, "GhRider");
                    MySleep(100,300);
                    if (UseSkillIf("Earthen Grip", (me.hpp < 50)))
                    {
                        Log("Earthen "+ me.hpp ,"GhRider" );
                    }
                    continue;
                }
                UseSkillIf("Absorb Lifeforce", (me.hpp < 66));

                //CLEAN DEBUF


                //FIGHT
                if (angle(me.target, me) > 45 && angle(me.target, me) < 315)
                    TurnDirectly(me.target);       //start by facing target

                if (UseSkillIf("Hell Spear",
                    (((targeCreature.hpp >= 33) && (me.hpp < 66)) || (getAggroMobs(me).Count > 1)) &&
                    (me.dist(me.target)<=8)        //only if in range
                    ))
                {
                    UseSkillIf("Summon Crows", (getAggroMobs(me).Count > 1)||(me.hpp<50));
                    UseSkillIf("Arc Lightning", (getAggroMobs(me).Count == 1));

                }
                    
                UseSkillIf("Freezing Arrow",me.dist(me.target) > 8);
                UseSkillIf("Insidious Whisper", me.dist(me.target) <= 8);
                UseSkillIf("Flamebolt");

                UseSkillIf("Freezing Earth",
                    (((targeCreature.hpp >= 33) && (me.hpp < 66)) || (getAggroMobs(me).Count > 1)) &&
                    (me.dist(me.target) <= 8));        //only if in range

                                                                              
                //BUFF
                UseSkillIf("Insulating Lens", (buffTime("Insulating Lens (Rank 1)") == 0 ));

            }
       }

        public void LootMob(Creature bestMob)
        {
            if (!GetGroupStatus("Corpse Loot")) return;
            while (bestMob != null && !isAlive(bestMob) && isExists(bestMob) && bestMob.type == BotTypes.Npc &&
                        ((Npc)bestMob).dropAvailable && isAlive())
                            {
                                if (me.dist(bestMob) > 3)
                                    ComeTo(bestMob, 1);
                                PickupAllDrop(bestMob);
                            }
        }

        public bool LootingEnabled
        {
            get { return true; }
        }
        

        public void PluginRun()
        {
            //new Task(() => { CancelAttacksOnAnothersMobs(); }).Start(); //Starting new thread
            SetGroupStatus("GhostRider", false);     //Is this thing or or what ?
            SetGroupStatus("Rest", true);            //Do we loot corpses or not ?
            SetGroupStatus("Farm", true);            //Do i keep on killing mobs ?
            SetGroupStatus("AFK", true); 
            while (true)
            {
                if (!me.isAlive())
                    DoResurrect();
                if (!GetGroupStatus("GhostRider") || !me.isAlive()) 
                    continue;
                //If GhostRider checkbox enabled in widget and our character alive
                //am i under attack (better way?) Or do i have someone targetted
                if ((GetGroupStatus("Farm")? getAggroMobs().Count > 0 : me.inFight) || (me.target != null && isAttackable(me.target) && isAlive(me.target)))
                {
                    if (me.target == null) SetTarget(getAggroMobs().First());
                    if (angle(me.target, me) > 45 && angle(me.target, me) < 315)
                        TurnDirectly(me.target);

                    if (isAlive(me.target))
                        DoRotation(me.target);
                    
                    MySleep(100,333);
                }
                //if (!me.isAlive()) continue;
                if (me.target != null && !me.target.isAlive())
                    LootMob(me.target);
                foreach (var m in getCreatures().Where(m=>m.dropAvailable && me.dist(m) < 5 ))
                {
                    PickupAllDrop(m);
                    MySleep(100,333);
                }                                           
                CheckBuffs();
                UseRegenItems();
                if (me.hpp > 66 && me.mpp >50) 
                    SearchForOtherTarget(me.target);
                if (GetGroupStatus("AFK")) DoAFK();
            }
        }

        private void DoAFK()
        {
            Processinventory();
        }

        private void DoResurrect()
        {

            Log("Dead: " + DateTime.Now, "GhRider");

            StopAllmove();
            MySleep(1000, 2000);
            
            while (!ResToRespoint())
                Thread.Sleep(10000);
            while (!SetTarget("Glorious Nui"))
                MySleep(500, 1000);
            
            if (me.target != null) MoveTo(me.target);

            RestoreExp();
            while ((me.isGlobalCooldown || me.isCasting))
                MySleep(100, 120);
        }

        private void MySleep(int frm, int to)
        {
            var rnd = new Random();
            var wait = rnd.Next(frm, to);
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
        

        private void SearchForOtherTarget(Creature target, bool keepattacking=false)
        {
            if (!GetGroupStatus("Farm")) return;
            try
            {
                SetTarget(GetBestNearestMob(target,55));
            }
            catch (Exception ex)
            {
                LogEx(ex);
            }
        }

        public void Processinventory  ()
        {
            foreach (var i in me.getItems().Where(i => i.place == ItemPlace.Bag && (i.id == 29203 || i.id == 29204 || i.id == 29205)))
            {
                while (i.count > 0 && me.opPoints > 150 && me.isAlive() && (GetGroupStatus("AFK")))
                    {
                        Thread.Sleep(150);
                        i.UseItem();
                        MySleep(10000,100000);
                        if (GetGroupStatus("GhostRider")) return;
                    }
                }
        }


        private void UseRegenItems()
        {
            if (!me.isAlive())
                return;
            if (me.inFight)                       
            {
                if (me.hpp < 60)
                {
                    var itemsToUse = me.getItems().FindAll(i => i.place == ItemPlace.Bag && (i.id == 18791 || i.id == 34006 || i.id == 34007));
                    foreach (var i in itemsToUse)
                        UseItemAndWait(i.id);
                }
                if (me.mpp < 70)
                {
                    var itemsToUse = me.getItems().FindAll(i => i.place == ItemPlace.Bag && (i.id == 18792 || i.id == 34008 || i.id == 34009));
                    foreach (var i in itemsToUse)
                        UseItemAndWait(i.id);
                }
            }
            else
            {
                if (me.hpp < 60)
                {
                    var itemsToUse = me.getItems().FindAll(i => i.place == ItemPlace.Bag && (i.id == 34003 || i.id == 34001 || i.id == 34000 ||  i.id == 17664 ));
                    foreach (var i in itemsToUse)
                        UseItemAndWait(i.id, true);
                }
                if (me.mpp < 70)
                {
                    var itemsToUse = me.getItems().FindAll(i => i.place == ItemPlace.Bag && (i.id == 34002 || i.id == 34005 || i.id == 34004 || i.id == 8505));
                    foreach (var i in itemsToUse)
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
            foreach (var i in me.getItems().Where(i => i.place == ItemPlace.Bag))
            {
                if (i.id == 29203 || i.id == 29204 || i.id == 29205)
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
                if (i.categoryId == 153 || itemsToUnseal.Contains(i.id)) //Unidentified or chests with closes.
                {
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
        }

        private void LogEx(Exception ex)
        {
            Log("!!##  Message = "+ ex.Message, "GhRider");
            Log("!!##  Source = " + ex.Source, "GhRider");
            Log("!!##  StackTrace = " + ex.StackTrace, "GhRider");
        }

        private List<uint> itemsToSell = new List<uint>() { 32110, 32134, 32102, 32123, 32099, 23387, 6127, 23390, 6152, 23388, 6077, 32166, 32113, 8012, 7992, 32145, 32152, 32170, 32175, 33000, 32169, 32176, 32173, 22103, 6202, 33213, 33005, 22159, 32184, 22230, 33226, 32996, 33224, 33361, 32984, 33233, 32981, 33234, 32978, 33225, 32990, 32987, 33235 };
        private List<uint> itemsToDelete = new List<uint>() { 15694, 29498, 19563, 17801, 19960, 14482, 17825, 15822, 14830, 23700, 26106, 16006, 21131, 24422 };
        private List<uint> itemsToWareHouse = new List<uint>() { 23092, 15596, 8337, 31892, 25253, 14677, 8343, 8000083, 8327, 23633, 21588, 16347, 16348, 16349, 16350, 16351, 16352, 23663 };
        private List<uint> itemsToUnseal = new List<uint>() { 32458, 32463, 32462, 32464, 33116, 33117, 33307, 33310, 33493, 33496, 33609, 33612, 33777, 33780 };
        private List<uint> itemsToDisenchant = new List<uint>() { 33440, 33350, 33361, 33360, 33362, 33374, 33370, 33369, 33351, 33371, 33466, 33468, 33467, 33439, 22132, 22763 };
          

    }
}