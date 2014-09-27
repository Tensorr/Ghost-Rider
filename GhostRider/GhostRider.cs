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
            return "0.2.4";
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

        public bool UseSkillAndWait(string skillName, bool selfTarget = false)
        {
            //wait cooldowns first, before we try to cast skill
            while (me.isCasting || me.isGlobalCooldown)
                MySleep(50,100);
            if (UseSkill(skillName, true, selfTarget))
            {
                //wait cooldown again, after we start cast skill
                while (me.isCasting)
                    Thread.Sleep(50);
                while (me.isGlobalCooldown)
                    Thread.Sleep(50);
                MySleep(250,500);
                return true;
            }
            if (me.target == null || GetLastError() != LastError.NoLineOfSight) return false;
            //No line of sight, try come to target.
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
            if (!cond || skillCooldown(skillName) != 0L) return false;
            try
            {
                return (UseSkillAndWait(skillName, selfTarget));
            }
            catch(Exception ex){
                LogEx(ex);
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
                if (!targeCreature.isAlive() || !isAttackable(targeCreature))
                {
                    if (getAggroMobs(me).Count == 0)
                        return;
                    try
                    {
                        targeCreature = getAggroMobs(me).First();
                    }
                    catch
                    {
                        return;
                    }
                }      

                var a=0;
                while (!SetTarget(targeCreature) && a < 20 && GetGroupStatus("GhostRider"))
                {
                    Thread.Sleep(50);
                    a++;
                }

                // one of these might throw an ex but the result is the same - retrun  
                try
                {
                    if (!me.isAlive()) return;
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
                else
                {
                    UseRegenItems();
                }
                UseSkillIf("Absorb Lifeforce", (me.hpp < 66));

                //CLEAN DEBUF


                //FIGHT
                //if (angle(me.target, me) > 45 && angle(me.target, me) < 315)
                    TurnDirectly(me.target);       //start by facing target
                try
                {
                    if (getAggroMobs(me).Count > 1 && TargetsWithin(8) > 1 && me.hpp < 75) //AOE First id i am not 100%
                    {
                        if (UseSkillIf("Summon Crows", UseSkillIf("Hell Spear", skillCooldown("Summon Crows") == 0L)))
                            continue;
                        if(UseSkillIf("Searing Rain", UseSkillIf("Freezing Earth", skillCooldown("Searing Rain") == 0L)))
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

                    UseSkillIf("Freezing Arrow");
                    UseSkillIf("Insidious Whisper", me.dist(me.target) <= 8);
                    UseSkillIf("Flamebolt", UseSkillIf("Flamebolt", UseSkillIf("Flamebolt")));

                    UseSkillIf("Freezing Earth",
                        (((targeCreature.hpp >= 33) && (me.hpp < 66)) || (getAggroMobs(me).Count > 1)) &&
                        (me.dist(me.target) <= 8)); //only if in range


                    //BUFF     //While fighting? 
                    UseSkillIf("Insulating Lens", (buffTime("Insulating Lens (Rank 3)") == 0 && me.hpp < 70));
                }
                catch (Exception ex)
                {
                    LogEx(ex);   
                }
            }
       }

        public void LootMob(Creature deadCreature)
        {
            while (deadCreature != null && !isAlive(deadCreature) && isExists(deadCreature) && deadCreature.type == BotTypes.Npc &&
                        ((Npc)deadCreature).dropAvailable && isAlive())
                            {
                                if (me.dist(deadCreature) > 3)
                                    ComeTo(deadCreature, 1);
                                PickupAllDrop(deadCreature);
                            }
        }

        public bool LootingEnabled
        {
            get { return true; }
        }

        public void PluginStop()
        {
            DelGroupStatus("GhostRider");      //Is this thing on or what ?
            DelGroupStatus("Inventory");       //Open Purses
            DelGroupStatus("Farm");            //Do i keep on killing mobs ?
            DelGroupStatus("AFK");             //If AFK is enabled some timings are made very relaxed (ie open a purse a minute or so) 
            DelGroupStatus("Eat/Drink");  
        }
        

        public void PluginRun()
        {
            //new Task(() => { CancelAttacksOnAnothersMobs(); }).Start(); //Starting new thread
            SetGroupStatus("GhostRider", false);      //Is this thing on or what ?
            SetGroupStatus("Inventory", false);       //Open Purses
            SetGroupStatus("Farm", false);            //Do i keep on killing mobs ?
            SetGroupStatus("AFK", false);             //If AFK is enabled some timings are made very relaxed (ie open a purse a minute or so) 
            SetGroupStatus("Eat/Drink", false);       //Eat after fighting, take potions during fight, drink bevs as buff
            while (true)
            {
                try{ if (!me.isAlive())
                        DoResurrect();}
                catch {}
                if (!GetGroupStatus("GhostRider") || !me.isAlive()) 
                    continue;
                //If GhostRider checkbox enabled in widget and our character alive
                //am i under attack (better way?) Or do i have someone targetted
                if ((GetGroupStatus("Farm")? getAggroMobs().Count > 0 : me.inFight) || (me.target != null && isAttackable(me.target) && isAlive(me.target)))
                {
                    try
                    {   if (me.target == null || getAggroMobs(me).Count > 0)
                            SetTarget(getAggroMobs(me).First());   // dont have a target selected, attack the one attacking me.
                        if (me.target == null && GetGroupStatus("Farm")) 
                            SetTarget(getAggroMobs().First());
                    }
                    catch
                    {
                        if  (getAggroMobs(me).Count > 0) SetTarget(getAggroMobs(me).First());
                        continue;
                    }

                    if ( me.target != null && (me.angle(me.target) > 45 && me.angle(me.target) < 315))
                        TurnDirectly(me.target);              //Face Target if not facing it.
                    
                    if (me.target != null && isAlive(me.target))
                        DoRotation(me.target);
                    
                    MySleep(100,333);
                }
                //if (!me.isAlive()) continue;
                try
                {
                    if (me.target != null && !me.target.isAlive())
                        LootMob(me.target);
                }
                catch {}
                foreach (var m in getCreatures().Where(m=>m.dropAvailable && me.dist(m) < 10 ))
                {
                    PickupAllDrop(m);
                    MySleep(100,333);
                }                                           
                CheckBuffs();
                UseRegenItems();
                if (me.hpp > 66 && me.mpp >50) 
                    SearchForOtherTarget(me.target);
                if (GetGroupStatus("Inventory")) Processinventory();
            }
        }

        private void DoAFK()
        {
            Processinventory();
        }

        private void DoResurrect()
        {

            Log("Dead: " + DateTime.Now, "GhRider");
            SetGroupStatus("Farm", false);
            StopAllmove();
            MySleep(1000, 2000);
            
            while (!ResToRespoint())
                Thread.Sleep(1000);
            while (!SetTarget("Glorious Nui"))
                MySleep(1500, 2500);
            
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
                SetTarget(GetBestNearestMob(target,40));
            }
            catch (Exception ex)
            {
                LogEx(ex);
            }
        }

        public void Processinventory  ()
        {
            foreach (
                var i in
                    me.getItems()
                        .Where(i => i.place == ItemPlace.Bag && (i.id == 29203 || i.id == 29204 || i.id == 29205)))
            {
                try
                {
                    if (i == null) continue;

                    while (i.count > 0 && me.opPoints > 150 && me.isAlive() && (GetGroupStatus("Inventory")))
                    {
                        Thread.Sleep(150);
                        i.UseItem();
                        if (GetGroupStatus("AFK")) MySleep(100000, 1000000);
                        MySleep(50, 150); // it's small enough so i dont care about conditionals
                        if (!GetGroupStatus("GhostRider")) continue;

                        return;
                    }
                }
                catch (Exception ex) { LogEx(ex);}
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
                    var itemsToUse = me.getItems().FindAll(i => i.place == ItemPlace.Bag && (i.id == 18791 || i.id == 34006 || i.id == 34007 || i.id == 15580));
                    foreach (var i in itemsToUse)
                        UseItemAndWait(i.id);
                }
                if (me.mpp < 50)
                {
                    var itemsToUse = me.getItems().FindAll(i => i.place == ItemPlace.Bag && (i.id == 18792 || i.id == 34008 || i.id == 34009 || i.id == 31770));
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
            foreach (
                var i in
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
                var i in
                    me.getItems()
                        .Where(i => i.place == ItemPlace.Bag && ((i.id == 29203 || i.id == 29204 || i.id == 29205))))
            {
                if (i.categoryId != 153 && !itemsToUnseal.Contains(i.id)) return;
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
            Log("!!##  Message = "+ ex.Message, "GhRider");
            Log("!!##  Source = " + ex.Source, "GhRider");
            Log("!!##  StackTrace = " + ex.StackTrace, "GhRider");
        }

        private List<uint> itemsToSell = new List<uint> { 32110, 32134, 32102, 32123, 32099, 23387, 6127, 23390, 6152, 23388, 6077, 32166, 32113, 8012, 7992, 32145, 32152, 32170, 32175, 33000, 32169, 32176, 32173, 22103, 6202, 33213, 33005, 22159, 32184, 22230, 33226, 32996, 33224, 33361, 32984, 33233, 32981, 33234, 32978, 33225, 32990, 32987, 33235 };
        private List<uint> itemsToDelete = new List<uint> { 15694, 29498, 19563, 17801, 19960, 14482, 17825, 15822, 14830, 23700, 26106, 16006, 21131, 24422 };
        private List<uint> itemsToWareHouse = new List<uint> { 23092, 15596, 8337, 31892, 25253, 14677, 8343, 8000083, 8327, 23633, 21588, 16347, 16348, 16349, 16350, 16351, 16352, 23663 };
        private List<uint> itemsToUnseal = new List<uint> { 32458, 32463, 32462, 32464, 33116, 33117, 33307, 33310, 33493, 33496, 33609, 33612, 33777, 33780 };
        private List<uint> itemsToDisenchant = new List<uint> { 33440, 33350, 33361, 33360, 33362, 33374, 33370, 33369, 33351, 33371, 33466, 33468, 33467, 33439, 22132, 22763 };
          

    }
}