from BaseClasses import Item


class SephiriaItem(Item):
    game = "Sephiria"


item_table = {
    "Ferocious Claws Reduction": 1,
    "Tenacious Body Reduction": 2,
    "Protective Armor Reduction": 3,
    "Agile Claws Reduction": 4,
    "Evil Energy Reduction": 5,
    "Combat Stance Reduction": 6,
    "Festival of Blood Reduction": 7,
    "False Vanguard Reduction": 8,
    "Dispossession Reduction": 9,
    "Dizziness Reduction": 10,
    "Deterioration Reduction": 11,
    "Tension Reduction": 12,
    "Heavy Heart Reduction": 13,
    "Decline Reduction": 14,
    "Severed Exchange Reduction": 15,
    "Rebellion of Machines Reduction": 16,
    "Proliferation Reduction": 17,
    "Nothing": 18,

    # Costumes
    "Fairy of Fortune": 19,
    "Pink Bunny": 20,
    "Armored Bunny": 21,
    "Brown Rabbit": 22,
    "Braided Hair Rabbit": 23,
    "Orange Rabbit": 24,
    "Red-Hooded Rabbit": 25,
    "White Rabbit": 26,
    "Wing-Eared Rabbit": 27,
    "Red-Clothed Cat": 28,
    "Red Fox": 29,
    "Frog": 30,
    "Mole": 31,
    "Otter": 32,
    "White Wolf": 33,
    "Wizard Bunny": 34,
    "Eagle": 35,
    "Crocodile": 36,
    "Deer": 37,
    "Farmer Squirrel": 38,
    "Turtle": 39,
    "Forest Cat": 40,
    "Ghost": 41,
    "Scholar Lizard": 42,
    "Wingless Bat": 43,
    "Skeleton": 44,
    "Adventurer Bunny": 45,

    # Permanent reward unlocks
    "Golden Needle": 46,
    "Boss Reward Unlock: Mogdi": 47,
    "Boss Reward Unlock: Rataka": 48,
    "Boss Reward Unlock: Erma": 49,
    "Boss Reward Unlock: Askard": 50,
    "Boss Reward Unlock: Odner": 51,
    "Boss Reward Unlock: Lerid": 52,
    "Boss Reward Unlock: Pentaxis": 53,
    "Boss Reward Unlock: Kovess": 54,
    "Boss Reward Unlock: Kraz": 55,
    "Boss Reward Unlock: Haz": 56,

    # Weapon unlocks
    "Greatsword Unlock": 57,
    "Dagger Unlock": 58,
    "Crossbow Unlock": 59,
    "Katana Unlock": 60,
    "Staff Unlock": 61,
    "Sword and Shield Unlock": 65,

    # Mission unlocks
    "Mission Unlock: Home Repairs": 62,
    "Mission Unlock: Fruit Skewer": 63,
    "Mission Unlock: Magic Workshop": 64,
    "Mission Unlock: Basement": 66,
    "Mission Unlock: Hide and Seek": 67,
    "Mission Unlock: Rylie's Gift": 68,
    "Mission Unlock: Terrain Recovery": 69,

    # Talent points
    "+5 Talent Points": 70,

    # Destiny Inscription upgrades
    "Tablet Unlock": 71,
    "Dice Upgrade I": 72,
    "Dice Acquisition": 73,
    "Seed Money": 74,
    "Chest of Possibilities": 75,
    "Wishing Fountain Upgrade": 76,
    "Dice Transformation": 77,

    "Level Up Restoration": 78,
    "Dice Upgrade II": 79,
    "Expanding Pouch I": 80,
    "Expanding Pouch II": 81,
    "Extra Experience Drop": 82,
    "MP Regeneration": 83,
    "Increase HP I": 84,
    "Increase HP II": 85,
    "Dice Upgrade III": 86,
    "Side Bag I": 87,
    "Side Bag II": 88,

    # Artifact unlock groups
    "Artifact Unlock I": 89,
    "Artifact Unlock II": 90,
    "Artifact Unlock III": 91,
    "Artifact Unlock IV": 92,
    "Artifact Unlock V": 93,
    "Artifact Unlock VI": 94,
    "Bond Artifact Unlock": 95,
    "Artifact Unlock VII": 96,
    "Artifact Unlock VIII": 97,

    # Mission Rewards
    "Potion Storage": 98,
    "Willpower Talent Unlock": 99,
    "Ingenuity Talent Unlock": 100,
    "Starting Potion": 101,
    "Costume Dyeing": 102,
    "Fruit Skewer Unlock": 103
}


# AP item name -> Sephiria CostumeEntity ID
costume_item_to_id = {
    "Pink Bunny": "PinkRabbit",
    "Armored Bunny": "ArmoredRabbit",
    "Brown Rabbit": "BrownRabbit",
    "Braided Hair Rabbit": "DarkBrownRabbit",
    "Orange Rabbit": "MandarinRabbit",
    "Red-Hooded Rabbit": "RedHolyRabbit",
    "White Rabbit": "WhiteRabbit",
    "Wing-Eared Rabbit": "HolyRabbit",
    "Red-Clothed Cat": "FireCat",
    "Red Fox": "Fox",
    "Frog": "Frog",
    "Mole": "Mole",
    "Otter": "Otter",
    "White Wolf": "WhiteWolf",
    "Wizard Bunny": "WizardRabbit",
    "Eagle": "Bird",
    "Crocodile": "Crocodile",
    "Deer": "Deer",
    "Fairy of Fortune": "Luck",
    "Farmer Squirrel": "Squirrel",
    "Turtle": "Turtle",
    "Forest Cat": "ForestCat",
    "Ghost": "Ghost",
    "Scholar Lizard": "Seren",
    "Wingless Bat": "Bat",
    "Skeleton": "Skeleton",
    "Adventurer Bunny": "AdventurerRabbit",
}


hard_mode_reduction_counts = {
    "Ferocious Claws Reduction": 7,
    "Tenacious Body Reduction": 7,
    "Protective Armor Reduction": 3,
    "Agile Claws Reduction": 2,
    "Evil Energy Reduction": 1,
    "Combat Stance Reduction": 1,
    "Festival of Blood Reduction": 1,
    "False Vanguard Reduction": 1,
    "Dispossession Reduction": 2,
    "Dizziness Reduction": 2,
    "Deterioration Reduction": 4,
    "Tension Reduction": 1,
    "Heavy Heart Reduction": 1,
    "Decline Reduction": 3,
    "Severed Exchange Reduction": 2,
    "Rebellion of Machines Reduction": 2,
    "Proliferation Reduction": 2,
}


# AP permanent reward item -> Sephiria TreeShopItemEntity ID
permanent_reward_item_to_tree_shop_id = {
    "Golden Needle": 3001,
    "Boss Reward Unlock: Mogdi": 2000,
    "Boss Reward Unlock: Rataka": 2001,
    "Boss Reward Unlock: Erma": 2002,
    "Boss Reward Unlock: Askard": 2003,
    "Boss Reward Unlock: Odner": 2004,
    "Boss Reward Unlock: Lerid": 2005,
    "Boss Reward Unlock: Pentaxis": 2006,
    "Boss Reward Unlock: Kovess": 2007,
    "Boss Reward Unlock: Kraz": 2008,
    "Boss Reward Unlock: Haz": 2009,
}

# Weapon unlock items
# AP item name -> Sephiria EWeaponType name
weapon_unlock_item_to_weapon_type = {
    "Sword and Shield Unlock": "SwordAndShield",
    "Greatsword Unlock": "GreatSword",
    "Dagger Unlock": "Dagger",
    "Crossbow Unlock": "Crossbow",
    "Staff Unlock": "Staff",
    "Katana Unlock": "Katana",
}

weapon_unlock_items = list(
    weapon_unlock_item_to_weapon_type.keys()
)


# Mission unlock items
mission_unlock_items = [
    "Mission Unlock: Home Repairs",
    "Mission Unlock: Fruit Skewer",
    "Mission Unlock: Magic Workshop",
    "Mission Unlock: Basement",
    "Mission Unlock: Hide and Seek",
    "Mission Unlock: Rylie's Gift",
    "Mission Unlock: Terrain Recovery",
]

# -------------------------------------------------
# Talent points
# -------------------------------------------------

talent_point_item_count = 10

# -------------------------------------------------
# Destiny Inscription upgrade items
# -------------------------------------------------

destiny_upgrade_item_to_tree_shop_id = {
    "Tablet Unlock": 0,
    "Dice Upgrade I": 4,
    "Dice Acquisition": 9,
    "Seed Money": 10,
    "Chest of Possibilities": 11,
    "Wishing Fountain Upgrade": 14,
    "Dice Transformation": 16,

    "Level Up Restoration": 302,
    "Dice Upgrade II": 303,
    "Expanding Pouch I": 304,
    "Expanding Pouch II": 305,
    "Extra Experience Drop": 306,
    "MP Regeneration": 309,
    "Increase HP I": 311,
    "Increase HP II": 312,
    "Dice Upgrade III": 313,
    "Side Bag I": 314,
    "Side Bag II": 315,

    "Artifact Unlock I": 1000,
    "Artifact Unlock II": 1001,
    "Artifact Unlock III": 1002,
    "Artifact Unlock IV": 1003,
    "Artifact Unlock V": 1004,
    "Artifact Unlock VI": 1005,
    "Bond Artifact Unlock": 1006,
    "Artifact Unlock VII": 1007,
    "Artifact Unlock VIII": 1008,
}


destiny_upgrade_item_counts = {
    "Tablet Unlock": 1,
    "Dice Upgrade I": 1,
    "Dice Acquisition": 1,
    "Seed Money": 2,
    "Chest of Possibilities": 1,
    "Wishing Fountain Upgrade": 1,
    "Dice Transformation": 1,

    "Level Up Restoration": 4,
    "Dice Upgrade II": 2,
    "Expanding Pouch I": 3,
    "Expanding Pouch II": 3,
    "Extra Experience Drop": 1,
    "MP Regeneration": 2,
    "Increase HP I": 2,
    "Increase HP II": 2,
    "Dice Upgrade III": 2,
    "Side Bag I": 3,
    "Side Bag II": 3,

    "Artifact Unlock I": 1,
    "Artifact Unlock II": 1,
    "Artifact Unlock III": 1,
    "Artifact Unlock IV": 1,
    "Artifact Unlock V": 1,
    "Artifact Unlock VI": 1,
    "Bond Artifact Unlock": 1,
    "Artifact Unlock VII": 1,
    "Artifact Unlock VIII": 1,
}

mission_reward_items = [
    "Potion Storage",
    "Willpower Talent Unlock",
    "Ingenuity Talent Unlock",
    "Starting Potion",
    "Costume Dyeing",
    "Fruit Skewer Unlock",
]