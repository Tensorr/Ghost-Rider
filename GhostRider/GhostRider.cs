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
            return "0.0.1a";
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
            //TODO: read array of buffs (foods?)
            if (buffTime("Insulating Lens (Rank 1)") == 0 && skillCooldown("Insulating Lens") == 0)
                UseSkillAndWait("Insulating Lens", true);
        }

        public void UseSkillAndWait(string skillName, bool selfTarget = false)
        {
            //wait cooldowns first, before we try to cast skill
            while (me.isCasting || me.isGlobalCooldown)
                Thread.Sleep(50);
            if (!UseSkill(skillName, true, selfTarget))
            {
                if (me.target != null && GetLastError() == LastError.NoLineOfSight)
                {
                    //No line of sight, try come to target.
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
            //wait cooldown again, after we start cast skill
            while (me.isCasting || me.isGlobalCooldown)
                Thread.Sleep(50);
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
            if (cond && skillCooldown(skillName)<=0L)
            {
                try { 
                    UseSkillAndWait(skillName, selfTarget);
                    return true; // too simple need to return true only on succesful cast
                    }
                catch(Exception ex){
                    //LOG ?
                    //
                }
            }
            return false;
        }
        /// <summary>
        /// Executes a simple rotation on the recieved target.
        ///  - Needs a valid target TODO:(or needs checks to verify if target is valid)
        /// </summary>
        /// <param name="targeCreature">Target to destroy (hopefully)</param>
        private void DoRotation(Creature targeCreature)
        {
            var aaa = false; //trash value

            while (GetGroupStatus("GhostRider"))
            {
                if (!me.isAlive() || !me.target.isAlive()) return;  
                // Be careful with spelling
                // SKILLS NEED TO BE ORDERED BY IMPORTANCE
                // IE: 1st : Heal cond: me.hp <33f
                // use: UseSkillIf if you need a  

                //HEAL
                if (UseSkillIf("Enervate", (me.hpp < 50)))
                    if (UseSkillIf("Earthen Grip", (me.hpp < 50)))
                        continue;
                                
                //CLEAN DEBUF

                //FIGHT
                if (UseSkillIf("Hell Spear", (hpp(targeCreature) >= 33) && ((TargetsWithin(6) > 1) || (me.hpp <75))))
                    aaa = UseSkillIf("Arc Lightning");

                aaa = UseSkillIf("Freezing Arrow");
                aaa = UseSkillIf("Flamebolt");

                //BUFF
                aaa= UseSkillIf("Insulating Lens", (buffTime("Insulating Lens (Rank 1)") == 0 ));

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
            get { return GetGroupStatus("Corpse Loot"); }
        }
        

        public void PluginRun()
        {
            //new Task(() => { CancelAttacksOnAnothersMobs(); }).Start(); //Starting new thread
            SetGroupStatus("GhostRider", false);        //Is this thing or or what ?
            SetGroupStatus("Corpse Loot", true);      //Do we loot corpses or not ?
            SetGroupStatus("Farm", true);            //Do i keep on killing mobs ?
            while (true)
            {
                if (!GetGroupStatus("GhostRider") || !me.isAlive()) continue;
                //If GhostRider checkbox enabled in widget and our character alive
                //am i under attack (better way?) Or do i have someone targetted
                if (getAggroMobs().Count > 0 || (me.target != null && isAttackable(me.target) && isAlive(me.target)))
                {
                    if (me.target == null) SetTarget(getAggroMobs().First());
                    if (angle(me.target, me) > 45 && angle(me.target, me) < 315)
                        TurnDirectly(me.target);

                    if (me.dist(me.target) < 25 && isAlive(me.target))
                        DoRotation(me.target);
                                   
                    //Small delay, do not load the processor
                    Thread.Sleep(10);
                }
                if (me.target != null)
                {
                    LootMob(me.target);
                    SearchForOtherTarget(me.target);
                }
                CheckBuffs();
            }
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
                SetTarget(GetBestNearestMob(target,20));
            }
            catch (Exception ex)
            {

                Log("!!##  Message = {0}", ex.Message);
                Log("!!##  Source = {0}", ex.Source);
                Log("!!##  StackTrace = {0}", ex.StackTrace);
            }
            
        }
    }
}