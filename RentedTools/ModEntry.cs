using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace RentedTools
{
    // NOTE: one might want to implement a static class `bool IsRentedTool(Item)` so some code can be reused

    public class ModEntry : Mod
    {
        private bool inited;
        private Farmer player;
        private NPC blacksmithNpc;
        private bool shouldCreateFailedToRentTools;
        private bool shouldCreateSucceededToRentTools;
        private bool rentedToolsOffered;
        private bool recycleOffered;


        private ITranslationHelper i18n;


        private List<Vector2> blackSmithCounterTiles = new List<Vector2>();

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += Bootstrap;
            helper.Events.Display.MenuChanged += MenuEventHandler;

            this.i18n = helper.Translation;
        }

        private void Bootstrap(object sender, EventArgs e)
        {
            // params reset
            this.inited = false;
            this.player = null;
            this.blacksmithNpc = null;

            this.shouldCreateFailedToRentTools = false;
            this.shouldCreateSucceededToRentTools = false;
            this.rentedToolsOffered = false;
            this.recycleOffered = false;

            this.blackSmithCounterTiles = new List<Vector2>();

            // params init
            this.player = Game1.player;
            this.blackSmithCounterTiles.Add(new Vector2(3f, 15f));
            foreach (NPC npc in Utility.getAllCharacters())
            {
                if (npc.Name == "Clint")
                {
                    this.blacksmithNpc = npc;
                    break;
                }
            }

            if (this.blacksmithNpc == null)
            {
                Monitor.Log("blacksmith NPC not found", LogLevel.Info);
            }

            // init done
            this.inited = true;
        }

        private Tool GetToolBeingUpgraded(Farmer who)
        {
            return who.toolBeingUpgraded.Value;
        }

        private void MenuEventHandler(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu == null && e.OldMenu != null)
            {
                // menu is closed
                MenuCloseHandler(sender, e);
            }
        }

        private void MenuCloseHandler(object sender, MenuChangedEventArgs e)
        {
            if (this.shouldCreateFailedToRentTools)
            {
                this.SetupFailedToRentDialog(this.player);
                this.shouldCreateFailedToRentTools = false;
            }
            else if (this.shouldCreateSucceededToRentTools)
            {
                this.SetupSucceededToRentDialog();
                this.shouldCreateSucceededToRentTools = false;
            } else if (this.rentedToolsOffered)
            {
                this.rentedToolsOffered = false;
            }
            else if (this.recycleOffered)
            {
                this.recycleOffered = false;
            }
            else if (this.inited && this.IsPlayerAtCounter(this.player))
            {
                if (this.player.toolBeingUpgraded.Value == null && this.HasRentedTools(this.player))
                {
                    this.SetupRentToolsRemovalDialog(this.player);
                }
                else if (this.ShouldOfferTools(this.player))
                {
                    this.SetupRentToolsOfferDialog(this.player);
                }
            }
        }

        private bool IsPlayerAtCounter(Farmer who)
        {
            return who.currentLocation.Name == "Blacksmith" && this.blackSmithCounterTiles.Contains(who.getTileLocation());
        }

        private bool HasRentedTools(Farmer who)
        {
            // Should recycle if:
            // (there's no tool being upgraded) and (there are tools of the same type)
            bool result = false;

            IList<Item> inventory = who.Items;
            List<Tool> tools = inventory
                .Where(tool => tool is Axe || tool is Pickaxe || tool is WateringCan || tool is Hoe)
                .OfType<Tool>()
                .ToList();

            if (GetToolBeingUpgraded(who) != null)
            {
                result = tools.Exists(item => item.GetType().IsInstanceOfType(this.GetToolBeingUpgraded(who)));
            }
            else
            {
                foreach (Tool tool in tools)
                {
                    if (tools.Exists(item => item.GetType().IsInstanceOfType(tool) && item.UpgradeLevel < tool.UpgradeLevel))
                    {
                        result = true;
                        break;
                    }
                }
            }

            return result;
        }

        private bool ShouldOfferTools(Farmer who)
        {
            return (GetToolBeingUpgraded(who) != null && !this.HasRentedTools(who));
        }

        private void SetupRentToolsRemovalDialog(Farmer who)
        {
            who.currentLocation.createQuestionDialogue(
                i18n.Get("Blacksmith_RecycleTools_Menu"),
                new Response[2]
                {
                    new Response("Confirm", i18n.Get("Blacksmith_RecycleToolsMenu_Confirm")),
                    new Response("Leave", i18n.Get("Blacksmith_RecycleToolsMenu_Leave")),
                },
                (Farmer whoInCallback, string whichAnswer) =>
                {
                    switch (whichAnswer)
                    {
                        case "Confirm":
                            this.RecycleTempTools(whoInCallback);
                            break;
                        case "Leave":
                            // do nothing
                            break;
                    }
                    return;
                },
                this.blacksmithNpc
            );
            this.recycleOffered = true;
        }

        private void SetupRentToolsOfferDialog(Farmer who)
        {
            who.currentLocation.createQuestionDialogue(
                i18n.Get("Blacksmith_OfferTools_Menu",
                new
                {
                    oldToolName = GetRentedToolByTool(GetToolBeingUpgraded(who))?.DisplayName,
                    newToolName = GetToolBeingUpgraded(who)?.DisplayName
                }),
                new Response[2]
                {
                    new Response("Confirm", i18n.Get("Blacksmith_OfferToolsMenu_Confirm")),
                    new Response("Leave", i18n.Get("Blacksmith_OfferToolsMenu_Leave")),
                },
                (Farmer whoInCallback, string whichAnswer) =>
                {
                    switch (whichAnswer)
                    {
                        case "Confirm":
                            this.BuyTempTool(whoInCallback);
                            break;
                        case "Leave":
                            // do nothing
                            break;
                    }
                    return;
                },
                this.blacksmithNpc
            );
            rentedToolsOffered = true;
        }

        private void SetupSucceededToRentDialog()
        {
            i18n.Get("Blacksmith_HowToReturn");
        }

        private void SetupFailedToRentDialog(Farmer who)
        {
            if (who.freeSpotsInInventory() <= 0)
            {
                Game1.drawObjectDialogue(i18n.Get("Blacksmith_NoInventorySpace"));
            }
            else
            {
                Game1.drawObjectDialogue(i18n.Get("Blacksmith_InsufficientFundsToRentTool"));
            }
        }

        private Tool GetRentedToolByTool(Item tool)
        {
            if (tool is Axe)
            {
                return new Axe();
            }
            else if (tool is Pickaxe)
            {
                return new Pickaxe();
            }
            else if (tool is WateringCan)
            {
                return new WateringCan();
            }
            else if (tool is Hoe)
            {
                return new Hoe();
            }
            else
            {
                Monitor.Log($"unsupported upgradable tool: {tool?.ToString()}");
                return null;
            }
        }

        private void BuyTempTool(Farmer who)
        {
            // NOTE: there's no thread safe method for money transactions, so I suppose the game doesn't care about it as well?

            // TODO: handle upgradeLevel so rented tool is not always the cheapest

            Item toolToBuy = this.GetRentedToolByTool(GetToolBeingUpgraded(who));

            if (toolToBuy == null)
            {
                return;
            }

            int toolCost = this.GetToolCost();

            if (who.Money >= toolCost && who.freeSpotsInInventory() > 0)
            {
                ShopMenu.chargePlayer(who, 0, toolCost);
                who.addItemToInventory(toolToBuy);
                this.shouldCreateSucceededToRentTools = true;
            }
            else
            {
                this.shouldCreateFailedToRentTools = true;
            }

        }

        private void RecycleTempTools(Farmer who)
        {
            // recycle all rented tools

            IList<Item> inventory = who.Items;
            List<Tool> tools = inventory
                .Where(tool => tool is Axe || tool is Pickaxe || tool is WateringCan || tool is Hoe)
                .OfType<Tool>()
                .ToList();

            foreach (Tool tool in tools)
            {
                if (tools.Exists(item => tool.GetType().IsInstanceOfType(item) && tool.UpgradeLevel < item.UpgradeLevel))
                {
                    who.removeItemFromInventory(tool);
                }
            }

            return;
        }

        private int GetToolCost()
        {
            // TODO: this function is subject to change
            return 200;
        }
    }
}
