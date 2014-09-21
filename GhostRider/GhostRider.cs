using System;
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
            return "0.1.0";
        }

        public static string GetPluginDescription()
        {
            return "Lazy Rider attempt, thanks @OUT";
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
                }
                catch { return; }
                // Be careful with spelling
                // SKILLS NEED TO BE ORDERED BY IMPORTANCE
                // IE: 1st : Heal cond: me.hp <33f
                // use: UseSkillIf if you need a  

                //HEAL
                if (UseSkillIf("Enervate", (me.hpp < 50)))
                {
                    
                }
                    if (UseSkillIf("Earthen Grip", (me.hpp < 50)))
                        continue;
                                
                //CLEAN DEBUF

                //FIGHT
                if (UseSkillIf("Hell Spear", (targeCreature.hpp >= 33) && ((TargetsWithin(4) > 1) || (me.hpp < 66))))
                    UseSkillIf("Arc Lightning");

                UseSkillIf("Freezing Arrow");
                UseSkillIf("Flamebolt");
                                                                              
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
            SetGroupStatus("GhostRider", false);        //Is this thing or or what ?
            SetGroupStatus("Rest", true);      //Do we loot corpses or not ?
            SetGroupStatus("Farm", true);            //Do i keep on killing mobs ?
            while (true)
            {
                if (!me.isAlive())
                    DoResurrect();
                if (!GetGroupStatus("GhostRider") || !me.isAlive()) 
                    continue;
                //If GhostRider checkbox enabled in widget and our character alive
                //am i under attack (better way?) Or do i have someone targetted
                if (getAggroMobs().Count > 0 || (me.target != null && isAttackable(me.target) && isAlive(me.target)))
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

            }
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

        private void UseRegenItems()
        {
            if (!me.isAlive())
                return;
            if (me.inFight)
            {
                //Банки, моментально юзаются
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
                //Печенье и т.п., юзается за 1-2 сек
                if (me.hpp < 60)
                {
                    var itemsToUse = me.getItems().FindAll(i => i.place == ItemPlace.Bag && (i.id == 34003 || i.id == 34001 || i.id == 34000));
                    foreach (var i in itemsToUse)
                        UseItemAndWait(i.id, true);
                }
                if (me.mpp < 70)
                {
                    var itemsToUse = me.getItems().FindAll(i => i.place == ItemPlace.Bag && (i.id == 34002 || i.id == 34005 || i.id == 34004));
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

        private void LogEx(Exception ex)
        {
            Log("!!##  Message = "+ ex.Message, "GhRider");
            Log("!!##  Source = " + ex.Source, "GhRider");
            Log("!!##  StackTrace = " + ex.StackTrace, "GhRider");
        }
    }
}