using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using SteamManagerRuntime;
using SteamManagerProtocol;
using UnityEngine;

namespace SteamManagerRuntimeTests
{
    public static partial class Entry
    {
        static int running;
        static Harmony patcher;
        static readonly List<string> passed=new List<string>();
        static readonly MethodInfo handle=AccessTools.Method(typeof(Bridge),"Handle");
        static readonly List<ItemDrop.ItemData> added=new List<ItemDrop.ItemData>();
        static readonly List<GameObject> spawned=new List<GameObject>();
        static Response Send(Request r){return (Response)handle.Invoke(null,new object[]{r});}
        static void Set(int id,bool enabled,float value=0){Send(new Request{Command="set",Id=id,Enabled=enabled,Value=value});}
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);passed.Add(label);}
        static void Near(float a,float b,string label){Check(Math.Abs(a-b)<0.02f,label);}
        static ItemDrop.ItemData Add(Player p,string prefab,int count)
        {
            var go=ObjectDB.instance.GetItemPrefab(prefab);if(go==null)throw new Exception("Missing test item "+prefab);
            var item=go.GetComponent<ItemDrop>().m_itemData.Clone();item.m_dropPrefab=go;item.m_stack=count;item.m_cheated=true;
            var inv=p.GetInventory();var slot=(Vector2i)AccessTools.Method(typeof(Inventory),"FindEmptySlot").Invoke(inv,new object[]{true});
            if(slot.x<0)throw new Exception("Test inventory is full.");
            var add=AccessTools.Method(typeof(Inventory),"AddItem",new[]{typeof(ItemDrop.ItemData),typeof(int),typeof(int),typeof(int),typeof(bool)});
            if(!(bool)add.Invoke(inv,new object[]{item,count,slot.x,slot.y,false}))throw new Exception("Test inventory add failed.");
            item=inv.GetItemAt(slot.x,slot.y);added.Add(item);return item;
        }
        static float Field(Player p,string name){return (float)AccessTools.Field(typeof(Player),name).GetValue(p);}
        static void Field(Player p,string name,float value){AccessTools.Field(typeof(Player),name).SetValue(p,value);}
        public static void Start()
        {
            patcher=new Harmony("LightSwornPVP.SteamManager.Validation");
            patcher.Patch(AccessTools.Method(typeof(Game),"Update"),postfix:new HarmonyMethod(typeof(Entry),"Run"));
        }
        public static void Run()
        {
            if(Interlocked.Exchange(ref running,1)!=0)return;
            Player p=Player.m_localPlayer;
            if(p==null){Finish("No test player.");return;}
            float health=p.GetHealth(),maxHealth=p.GetMaxHealth(),stamina=p.GetStamina(),maxStamina=p.GetMaxStamina(),eitr=p.GetEitr(),maxEitr=p.GetMaxEitr();
            float foodTimer=Field(p,"m_foodUpdateTimer");
            var foods=new List<Player.Food>(p.GetFoods());
            Skills.Skill skill=null;float skillLevel=0,skillAccumulator=0;
            StatusEffect rested=null;string error=null;
            try
            {
                if(!ZNet.instance.IsServer()||ZNet.IsOpenServer())throw new Exception("Probe requires disposable single-player world.");
                Bridge.Reset();
                p.SetHealth(Math.Max(health,20));float before=p.GetHealth();
                var hit=new HitData();hit.m_damage.m_damage=1;
                p.ApplyDamage(hit,false,false);Check(p.GetHealth()<before,"Normal health damage executes");
                Set(2,true);before=p.GetHealth();p.ApplyDamage(hit,false,false);Near(p.GetHealth(),before,"Unlimited health blocks ApplyDamage");Set(2,false);
                Field(p,"m_stamina",50);p.UseStamina(5);Check(p.GetStamina()<50,"Normal stamina spending executes");
                Set(3,true);before=p.GetStamina();p.UseStamina(5);Near(p.GetStamina(),before,"Unlimited stamina prevents spending");Check(p.HaveStamina(10000),"Unlimited stamina passes ability affordability");Set(3,false);
                Field(p,"m_eitr",50);p.UseEitr(5);Check(p.GetEitr()<50,"Normal Eitr spending executes");
                Set(4,true);before=p.GetEitr();p.UseEitr(5);Near(p.GetEitr(),before,"Unlimited Eitr prevents spending");Check(p.HaveEitr(10000),"Unlimited Eitr passes ability affordability");Set(4,false);
                var fall=new HitData();fall.m_damage.m_damage=1;fall.m_hitType=HitData.HitType.Fall;
                Set(9,true);before=p.GetHealth();p.Damage(fall);Near(p.GetHealth(),before,"Fall damage blocked at local damage entry point");Set(9,false);

                p.GetFoods().Clear();var berry=Add(p,"Raspberry",4);p.EatFood(berry);var food=p.GetFoods()[0];
                var updateFood=AccessTools.Method(typeof(Player),"UpdateFood");
                before=food.m_time;updateFood.Invoke(p,new object[]{0f,true});Near(food.m_time,before-1,"Normal food timer advances");
                Set(15,true,0);before=food.m_time;updateFood.Invoke(p,new object[]{0f,true});Near(food.m_time,before,"Food freeze retains remaining duration");
                Set(15,true,2);before=food.m_time;updateFood.Invoke(p,new object[]{0f,true});Near(food.m_time,before-0.5f,"Food duration multiplier halves timer loss");Set(15,false,0);
                rested=ScriptableObject.CreateInstance<SE_Rested>();rested.m_character=p;rested.m_ttl=100;
                var effectTime=AccessTools.Field(typeof(StatusEffect),"m_time");
                rested.UpdateStatusEffect(1);Near((float)effectTime.GetValue(rested),1,"Normal rested timer advances");
                Set(16,true,0);rested.UpdateStatusEffect(1);Near((float)effectTime.GetValue(rested),1,"Rested freeze retains duration");
                Set(16,true,2);rested.UpdateStatusEffect(1);Near((float)effectTime.GetValue(rested),1.5f,"Rested duration multiplier halves timer loss");Set(16,false,0);

                p.GetFoods().Clear();before=berry.m_stack;p.ConsumeItem(p.GetInventory(),berry);Near(berry.m_stack,before-1,"Normal consumable use spends an item");
                p.GetFoods().Clear();Set(29,true);before=berry.m_stack;p.ConsumeItem(p.GetInventory(),berry);Near(berry.m_stack,before,"Unlimited items retains consumed food");Set(29,false);
                var torch=Add(p,"Torch",1);torch.m_durability=2;
                var drain=AccessTools.Method(typeof(Humanoid),"DrainEquipedItemDurability");drain.Invoke(p,new object[]{torch,1f});Check(torch.m_durability<2,"Normal equipment durability drains");
                Set(30,true);before=torch.m_durability;drain.Invoke(p,new object[]{torch,1f});Near(torch.m_durability,before,"Unlimited durability blocks equipment drain");Set(30,false);
                torch.m_durability=1;Send(new Request{Command="repair"});Near(torch.m_durability,torch.GetMaxDurability(),"Repair action restores damaged equipment");

                skill=(Skills.Skill)AccessTools.Method(typeof(Skills),"GetSkill").Invoke(p.GetSkills(),new object[]{Skills.SkillType.Run});skillLevel=skill.m_level;skillAccumulator=skill.m_accumulator;
                skill.m_level=10;skill.m_accumulator=0;p.GetSkills().RaiseSkill(Skills.SkillType.Run,0.1f);float normalXP=skill.m_accumulator;Check(normalXP>0,"Normal skill XP accrues");
                skill.m_accumulator=0;Set(34,true,3);p.GetSkills().RaiseSkill(Skills.SkillType.Run,0.1f);Near(skill.m_accumulator,normalXP*3,"Skill multiplier applies to earned XP");Set(34,false,2);

                var available=new List<Recipe>();p.GetAvailableRecipes(ref available);int known=available.Count;
                Set(37,true);p.GetAvailableRecipes(ref available);Check(available.Count>known,"Blueprint mode exposes additional recipes");Set(37,false);

                var creaturePrefab=ZNetScene.instance.GetPrefab("Greydwarf");if(creaturePrefab==null)throw new Exception("Missing test creature.");
                var go=UnityEngine.Object.Instantiate(creaturePrefab,p.transform.position+new Vector3(0,0,40),Quaternion.identity);spawned.Add(go);
                var ai=go.GetComponent<BaseAI>();if(ai!=null)ai.enabled=false;
                var enemy=go.GetComponent<Character>();enemy.SetHealth(enemy.GetMaxHealth());
                var strike=new HitData();strike.m_damage.m_blunt=1;strike.SetAttacker(p);
                before=enemy.GetHealth();enemy.Damage(strike);float baseDamage=before-enemy.GetHealth();Check(baseDamage>0,"Normal outgoing creature damage executes");
                strike=new HitData();strike.m_damage.m_blunt=1;strike.SetAttacker(p);before=enemy.GetHealth();Set(23,true,3);enemy.Damage(strike);Near(before-enemy.GetHealth(),baseDamage*3,"Outgoing damage multiplier changes actual creature damage");Set(23,false,1);
                strike=new HitData();strike.m_damage.m_blunt=1;strike.SetAttacker(p);Set(25,true);enemy.Damage(strike);Check(enemy.GetHealth()<=0,"One-hit mode kills the test creature");Set(25,false);
                ExtendedChecks(p);
            }
            catch(Exception e){error=e.GetBaseException().Message;}
            finally
            {
                try
                {
                    Bridge.Reset();
                    if(skill!=null){skill.m_level=skillLevel;skill.m_accumulator=skillAccumulator;}
                    p.GetFoods().Clear();p.GetFoods().AddRange(foods);Field(p,"m_foodUpdateTimer",foodTimer);
                    p.SetMaxHealth(maxHealth,false);p.SetMaxStamina(maxStamina,false);AccessTools.Method(typeof(Player),"SetMaxEitr").Invoke(p,new object[]{maxEitr,false});p.SetHealth(health);Field(p,"m_stamina",stamina);Field(p,"m_eitr",eitr);
                    foreach(var item in added)if(p.GetInventory().ContainsItem(item))p.GetInventory().RemoveItem(item);
                    foreach(var go in spawned)if(go!=null)ZNetScene.instance.Destroy(go);
                    if(rested!=null)UnityEngine.Object.Destroy(rested);
                }
                catch(Exception e){error=(error??"")+" Cleanup: "+e.GetBaseException().Message;}
                Finish(error);
            }
        }
        static void Finish(string error)
        {
            try{patcher.UnpatchAll("LightSwornPVP.SteamManager.Validation");}catch{}
            var result=new Response{Ok=error==null,Message=error??"All runtime behavior probes passed.",Entries=passed.ConvertAll(x=>new SteamManagerProtocol.Entry{Name=x})};
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(typeof(Entry).Assembly.Location),"behavior-results.json"),Wire.Encode(result));
        }
    }
}
