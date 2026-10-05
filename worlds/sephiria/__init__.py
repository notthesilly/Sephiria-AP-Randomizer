from BaseClasses import Region, ItemClassification
from worlds.AutoWorld import World, WebWorld

from .Options import SephiriaOptions

from .Items import (
    SephiriaItem,
    item_table,
    hard_mode_reduction_counts,
    costume_item_to_id,
    permanent_reward_item_to_tree_shop_id,
    weapon_unlock_items,
    weapon_unlock_item_to_weapon_type,
    mission_unlock_items,
    talent_point_item_count,
    destiny_upgrade_item_counts,
    mission_reward_items
)

from .Locations import (
    SephiriaLocation,
    get_sephirite_check_count,
    location_table,
    shrine_check_counts,
    chapter_floor_counts,
    floor_names,
    costume_mission_location_table,
    destiny_inscription_location_table,
    mission_location_table,
    boss_defeat_location_table,
    boss_defeat_floor_table
)


class SephiriaWebWorld(WebWorld):
    pass


class SephiriaWorld(World):
    game = "Sephiria"
    web = SephiriaWebWorld()

    options_dataclass = SephiriaOptions

    item_name_to_id = item_table
    location_name_to_id = location_table

    def create_regions(self):
        menu = Region(
            "Menu",
            self.player,
            self.multiworld
        )

        self.multiworld.regions.append(menu)

        goal_chapter = self.options.goal_chapter.value

        shrine_count = shrine_check_counts[goal_chapter]

        for i in range(1, shrine_count + 1):
            location_name = (
                f"Archipelago Shrine Check {i:02d}"
            )

            location = SephiriaLocation(
                self.player,
                location_name,
                location_table[location_name],
                menu
            )

            menu.locations.append(location)

        floor_count = chapter_floor_counts[
            goal_chapter
        ]

        for floor_number in range(
            1,
            floor_count + 1
        ):
            floor_name = floor_names[floor_number]

            floor_check_count = get_sephirite_check_count(floor_number)

            for item_number in range(
                1,
                floor_check_count + 1
            ):
                location_name = (
                    f"{floor_name} Sephirite Item "
                    f"{item_number}"
                )

                location = SephiriaLocation(
                    self.player,
                    location_name,
                    location_table[location_name],
                    menu
                )

                menu.locations.append(location)

        # -------------------------------------------------
        # Costume mission checks
        # -------------------------------------------------

        for location_name, location_id in (
            costume_mission_location_table.items()
        ):
            location = SephiriaLocation(
                self.player,
                location_name,
                location_id,
                menu
            )

            menu.locations.append(location)

        # -------------------------------------------------
        # Destiny Inscription checks
        # -------------------------------------------------

        for location_name, location_id in (
            destiny_inscription_location_table.items()
        ):
            location = SephiriaLocation(
                self.player,
                location_name,
                location_id,
                menu
            )

            menu.locations.append(location)

        # -------------------------------------------------
        # Mission completion checks
        # -------------------------------------------------

        for location_name, location_id in (
            mission_location_table.items()
        ):
            location = SephiriaLocation(
                self.player,
                location_name,
                location_id,
                menu
            )

            menu.locations.append(location)

        # -------------------------------------------------
        # Boss defeat checks
        # -------------------------------------------------

        for location_name, location_id in (
            boss_defeat_location_table.items()
        ):
            boss_floor = boss_defeat_floor_table[
                location_name
            ]

            # Only include bosses from floors that exist
            # for the selected goal chapter.
            if boss_floor > floor_count:
                continue

            location = SephiriaLocation(
                self.player,
                location_name,
                location_id,
                menu
            )

            menu.locations.append(location)

    def create_items(self):
        goal_chapter = self.options.goal_chapter.value

        shrine_count = shrine_check_counts[goal_chapter]
        floor_count = chapter_floor_counts[goal_chapter]

        sephirite_count = sum(
            get_sephirite_check_count(floor_number)
            for floor_number in range(
                1,
                floor_count + 1
            )
        )

        costume_mission_count = len(costume_mission_location_table)
        destiny_inscription_count = len(destiny_inscription_location_table)
        mission_count = len(mission_location_table)

        boss_defeat_count = sum(
            1
            for location_name in boss_defeat_location_table
            if boss_defeat_floor_table[
                location_name
            ] <= floor_count
        )

        total_location_count = (
            shrine_count +
            sephirite_count +
            costume_mission_count +
            destiny_inscription_count +
            mission_count +
            boss_defeat_count
        )

        # -------------------------------------------------
        # Hard Mode reduction items
        # -------------------------------------------------

        reduction_pool = []

        for item_name, count in hard_mode_reduction_counts.items():
            reduction_pool.extend(
                [item_name] * count
            )

        self.random.shuffle(reduction_pool)

        for item_name in reduction_pool:
            item = SephiriaItem(
                item_name,
                ItemClassification.useful,
                item_table[item_name],
                self.player
            )

            self.multiworld.itempool.append(item)

        # -------------------------------------------------
        # Costumes
        # -------------------------------------------------

        costume_names = list(
            costume_item_to_id.keys()
        )

        # Every seed gives the player exactly one random
        # starting costume.
        self.starting_costume_name = (
            self.random.choice(costume_names)
        )

        self.starting_costume_id = (
            costume_item_to_id[
                self.starting_costume_name
            ]
        )

        # Every costume except the starting costume
        # becomes an Archipelago item.
        for costume_name in costume_names:
            if costume_name == self.starting_costume_name:
                continue

            item = SephiriaItem(
                costume_name,
                ItemClassification.useful,
                item_table[costume_name],
                self.player
            )

            self.multiworld.itempool.append(item)

        # -------------------------------------------------
        # Permanent reward unlocks
        # -------------------------------------------------

        for item_name in permanent_reward_item_to_tree_shop_id:
            item = SephiriaItem(
                item_name,
                ItemClassification.useful,
                item_table[item_name],
                self.player
            )

            self.multiworld.itempool.append(item)

        # -------------------------------------------------
        # Weapon unlocks
        # -------------------------------------------------

        # Every seed gives the player exactly one random
        # starting weapon from the six AP weapons.
        self.starting_weapon_item_name = (
            self.random.choice(weapon_unlock_items)
        )

        self.starting_weapon_type = (
            weapon_unlock_item_to_weapon_type[
                self.starting_weapon_item_name
            ]
        )

        # Every weapon except the starting weapon becomes
        # an Archipelago progression item.
        for item_name in weapon_unlock_items:
            if item_name == self.starting_weapon_item_name:
                continue

            item = SephiriaItem(
                item_name,
                ItemClassification.progression,
                item_table[item_name],
                self.player
            )

            self.multiworld.itempool.append(item)

        # -------------------------------------------------
        # Mission unlocks
        # -------------------------------------------------

        for item_name in mission_unlock_items:
            item = SephiriaItem(
                item_name,
                ItemClassification.progression,
                item_table[item_name],
                self.player
            )

            self.multiworld.itempool.append(item)

        # -------------------------------------------------
        # Mission Rewards
        # -------------------------------------------------

        junk_mission_reward_items = {
            "Costume Dyeing"
        }

        for item_name in mission_reward_items:
            classification = (
                ItemClassification.filler
                if item_name in junk_mission_reward_items
                else ItemClassification.useful
            )

            self.multiworld.itempool.append(
                SephiriaItem(
                    item_name,
                    classification,
                    item_table[item_name],
                    self.player
                )
            )

        # -------------------------------------------------
        # Talent points
        # -------------------------------------------------

        for _ in range(talent_point_item_count):
            self.multiworld.itempool.append(
                SephiriaItem(
                    "+5 Talent Points",
                    ItemClassification.useful,
                    item_table["+5 Talent Points"],
                    self.player
                )
            )

        # -------------------------------------------------
        # Destiny Inscription upgrades
        # -------------------------------------------------

        for item_name, item_count in (
            destiny_upgrade_item_counts.items()
        ):
            for _ in range(item_count):
                self.multiworld.itempool.append(
                    SephiriaItem(
                        item_name,
                        ItemClassification.useful,
                        item_table[item_name],
                        self.player
                    )
                )

        # -------------------------------------------------
        # Fill remaining locations
        # -------------------------------------------------

        costume_item_count = len(costume_names) - 1
        permanent_reward_item_count = len(permanent_reward_item_to_tree_shop_id)
        weapon_unlock_item_count = len(weapon_unlock_items) - 1
        mission_unlock_item_count = len(mission_unlock_items)
        destiny_upgrade_item_count = sum(destiny_upgrade_item_counts.values())
        mission_reward_item_count = len(mission_reward_items)

        required_item_count = (
            len(reduction_pool) +
            costume_item_count +
            permanent_reward_item_count +
            weapon_unlock_item_count +
            mission_unlock_item_count +
            mission_reward_item_count +
            talent_point_item_count +
            destiny_upgrade_item_count
        )

        if total_location_count < required_item_count:
            raise Exception(
                "Not enough locations for all "
                "Archipelago items."
            )

        nothing_count = (
            total_location_count -
            required_item_count
        )

        for _ in range(nothing_count):
            item = SephiriaItem(
                "Nothing",
                ItemClassification.filler,
                item_table["Nothing"],
                self.player
            )

            self.multiworld.itempool.append(item)

    def fill_slot_data(self):
        return {
            "goal_chapter":
                self.options.goal_chapter.value,

            "required_chapter_clears":
                self.options.required_chapter_clears.value,

            "unique_weapons_required":
                bool(
                    self.options
                        .unique_weapons_required
                        .value
                ),

            "starting_costume":
                self.starting_costume_id,

            "starting_weapon":
                self.starting_weapon_type
        }
