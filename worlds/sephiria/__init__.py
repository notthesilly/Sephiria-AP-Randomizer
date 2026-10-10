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
    mission_reward_items,
    progressive_anvil_unlock_item_count,
    trap_item_counts
)

from .Locations import (
    SephiriaLocation,
    get_sephirite_check_count,
    location_table,
    shrine_check_counts,
    chapter_floor_counts,
    floor_names,
    late_floor_start_by_goal_chapter,
    early_destiny_inscription_candidates,
    late_destiny_inscription_locations,
    costume_mission_location_table,
    destiny_inscription_location_table,
    mission_location_table,
    boss_defeat_location_table,
    boss_defeat_floor_table,
    MAX_SEPHIRITE_CHECKS_PER_FLOOR
)


from BaseClasses import (
    Region,
    ItemClassification,
    LocationProgressType
)

from worlds.generic.Rules import (
    add_item_rule,
    set_rule
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

        sephirites_per_floor = (
            self.options.sephirites_per_floor.value
        )

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

            floor_check_count = get_sephirite_check_count(floor_number, sephirites_per_floor)

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


        # -------------------------------------------------
        # Random early multiworld priority locations
        # -------------------------------------------------

        mole_early_candidates = [
            (
                "Mole Stronghold Sephirite Item "
                f"{item_number}"
            )
            for item_number in range(
                1,
                get_sephirite_check_count(
                    1,
                    sephirites_per_floor
                ) + 1
            )
        ]

        self.early_mole_locations = (
            self.random.sample(
                mole_early_candidates,
                min(3, len(mole_early_candidates))
            )
        )

        self.early_destiny_locations = (
            self.random.sample(
                early_destiny_inscription_candidates,
                min(
                    3,
                    len(
                        early_destiny_inscription_candidates
                    )
                )
            )
        )


    def create_items(self):
        goal_chapter = self.options.goal_chapter.value

        sephirites_per_floor = (
            self.options.sephirites_per_floor.value
        )

        randomize_anvil_unlocks = bool(
            self.options.randomize_anvil_unlocks.value
        )

        starting_cintamani_stone_count = (
            self.options
                .starting_cintamani_stones
                .value
        )

        traps_enabled = bool(
            self.options.traps.value
        )

        shrine_count = shrine_check_counts[
            goal_chapter
        ]

        floor_count = chapter_floor_counts[
            goal_chapter
        ]

        sephirite_count = sum(
            get_sephirite_check_count(
                floor_number,
                sephirites_per_floor
            )
            for floor_number in range(
                1,
                floor_count + 1
            )
        )

        # -------------------------------------------------
        # Available permanent reward items
        # -------------------------------------------------

        available_permanent_reward_items = []

        boss_reward_prefix = (
            "Boss Reward Unlock: "
        )

        for item_name in (
            permanent_reward_item_to_tree_shop_id
        ):
            if item_name.startswith(
                boss_reward_prefix
            ):
                boss_name = item_name[
                    len(boss_reward_prefix):
                ]

                boss_location_name = (
                    f"Boss Defeat - {boss_name}"
                )

                boss_floor = (
                    boss_defeat_floor_table.get(
                        boss_location_name
                    )
                )

                if boss_floor is None:
                    raise Exception(
                        "No boss floor mapping exists for "
                        f"{item_name}."
                    )

                # Do not add rewards for bosses that cannot
                # appear with the selected Goal Chapter.
                if boss_floor > floor_count:
                    continue

            available_permanent_reward_items.append(
                item_name
            )

        costume_names = list(costume_item_to_id)

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
        # Required item count
        # -------------------------------------------------

        reduction_item_count = sum(
            hard_mode_reduction_counts.values()
        )

        costume_item_count = (
            len(costume_names) - 1
        )

        permanent_reward_item_count = len(
            available_permanent_reward_items
        )

        weapon_unlock_item_count = (
            len(weapon_unlock_items) - 1
        )

        mission_unlock_item_count = len(
            mission_unlock_items
        )

        mission_reward_item_count = len(
            mission_reward_items
        )

        destiny_upgrade_item_count = sum(
            destiny_upgrade_item_counts.values()
        )

        progressive_anvil_unlock_count = (
            progressive_anvil_unlock_item_count
            if randomize_anvil_unlocks
            else 0
        )

        trap_item_count = (
            sum(trap_item_counts.values())
            if traps_enabled
            else 0
        )

        required_item_count = (
            reduction_item_count +
            costume_item_count +
            permanent_reward_item_count +
            weapon_unlock_item_count +
            mission_unlock_item_count +
            mission_reward_item_count +
            talent_point_item_count +
            destiny_upgrade_item_count +
            progressive_anvil_unlock_count +
            starting_cintamani_stone_count +
            trap_item_count
        )

        # -------------------------------------------------
        # Validate Sephirite count
        # -------------------------------------------------

        non_sephirite_location_count = (
            total_location_count -
            sephirite_count
        )

        minimum_sephirites_per_floor = None

        for candidate in range(8, MAX_SEPHIRITE_CHECKS_PER_FLOOR + 1):
            candidate_sephirite_count = sum(
                get_sephirite_check_count(
                    floor_number,
                    candidate
                )
                for floor_number in range(
                    1,
                    floor_count + 1
                )
            )

            if (
                non_sephirite_location_count +
                candidate_sephirite_count >=
                required_item_count
            ):
                minimum_sephirites_per_floor = (
                    candidate
                )

                break

        if minimum_sephirites_per_floor is None:
            raise Exception(
                "The selected Sephiria configuration "
                "cannot provide enough locations even "
                f"with {MAX_SEPHIRITE_CHECKS_PER_FLOOR} "
                "Sephirites Per Floor."
            )

        if (
            sephirites_per_floor <
            minimum_sephirites_per_floor
        ):
            raise Exception(
                "Sephirites Per Floor must be at least "
                f"{minimum_sephirites_per_floor} "
                f"for Goal Chapter {goal_chapter} "
                "with the selected options."
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

        for item_name in available_permanent_reward_items:
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
            classification = (
                ItemClassification.progression
                if item_name in {
                    "Tablet Unlock",
                    "Progressive Artifact Unlock",
                    "Bond Artifact Unlock"
                }
                else ItemClassification.useful
            )

            for _ in range(item_count):
                self.multiworld.itempool.append(
                    SephiriaItem(
                        item_name,
                        classification,
                        item_table[item_name],
                        self.player
                    )
                )

        # Keep Tablet Unlock early in the multiworld.
        self.multiworld.early_items[
            self.player
        ]["Tablet Unlock"] = 1

        # -------------------------------------------------
        # Progressive Anvil unlocks
        # -------------------------------------------------

        for _ in range(
            progressive_anvil_unlock_count
        ):
            self.multiworld.itempool.append(
                SephiriaItem(
                    "Progressive Anvil Unlock",
                    ItemClassification.progression,
                    item_table[
                        "Progressive Anvil Unlock"
                    ],
                    self.player
                )
            )

        # -------------------------------------------------
        # Starting Cintamani Stones
        # -------------------------------------------------

        for _ in range(
            starting_cintamani_stone_count
        ):
            self.multiworld.itempool.append(
                SephiriaItem(
                    "Starting Cintamani Stone",
                    ItemClassification.progression,
                    item_table[
                        "Starting Cintamani Stone"
                    ],
                    self.player
                )
            )

        # -------------------------------------------------
        # Traps
        # -------------------------------------------------

        if traps_enabled:
            for item_name, item_count in (
                trap_item_counts.items()
            ):
                for _ in range(item_count):
                    self.multiworld.itempool.append(
                        SephiriaItem(
                            item_name,
                            ItemClassification.trap,
                            item_table[item_name],
                            self.player
                        )
                    )

        # -------------------------------------------------
        # Fill remaining locations
        # -------------------------------------------------

        if total_location_count < required_item_count:
            raise Exception(
                "Not enough locations for all "
                "Archipelago items."
            )

        sapphire_5_count = (
            total_location_count -
            required_item_count
        )

        for _ in range(sapphire_5_count):
            item = SephiriaItem(
                "5 Sapphires",
                ItemClassification.filler,
                item_table["5 Sapphires"],
                self.player
            )

            self.multiworld.itempool.append(item)


    def set_rules(self):
        # -------------------------------------------------
        # Early multiworld priority locations
        # -------------------------------------------------

        early_priority_locations = (
            self.early_mole_locations +
            self.early_destiny_locations
        )

        for location_name in early_priority_locations:
            location = self.multiworld.get_location(
                location_name,
                self.player
            )

            location.progress_type = (
                LocationProgressType.PRIORITY
            )


        # -------------------------------------------------
        # Late-floor protection
        # -------------------------------------------------

        goal_chapter = (
            self.options.goal_chapter.value
        )

        late_floor_start = (
            late_floor_start_by_goal_chapter[
                goal_chapter
            ]
        )

        floor_count = (
            chapter_floor_counts[
                goal_chapter
            ]
        )

        sephirites_per_floor = (
            self.options
                .sephirites_per_floor
                .value
        )

        for floor_number in range(
            late_floor_start,
            floor_count + 1
        ):
            floor_name = (
                floor_names[
                    floor_number
                ]
            )

            floor_check_count = (
                get_sephirite_check_count(
                    floor_number,
                    sephirites_per_floor
                )
            )

            for item_number in range(
                1,
                floor_check_count + 1
            ):
                location_name = (
                    f"{floor_name} Sephirite Item "
                    f"{item_number}"
                )

                location = (
                    self.multiworld.get_location(
                        location_name,
                        self.player
                    )
                )

                add_item_rule(
                    location,
                    lambda item:
                        not item.advancement
                )


        # -------------------------------------------------
        # Late boss protection
        # -------------------------------------------------

        for (
            location_name,
            boss_floor
        ) in boss_defeat_floor_table.items():

            if boss_floor < late_floor_start:
                continue

            # Boss may not exist for the selected chapter.
            if boss_floor > floor_count:
                continue

            location = (
                self.multiworld.get_location(
                    location_name,
                    self.player
                )
            )

            add_item_rule(
                    location,
                    lambda item:
                        not item.advancement
                )



        # -------------------------------------------------
        # Shrine protection
        # -------------------------------------------------

        shrine_count = shrine_check_counts[
            goal_chapter
        ]

        for i in range(1, shrine_count + 1):
            location_name = (
                f"Archipelago Shrine Check {i:02d}"
            )

            location = (
                self.multiworld.get_location(
                    location_name,
                    self.player
                )
            )

            add_item_rule(
                    location,
                    lambda item:
                        not item.advancement
                )


        # -------------------------------------------------
        # Late Destiny Inscription protection
        # -------------------------------------------------

        for location_name in (
            late_destiny_inscription_locations
        ):
            location = (
                self.multiworld.get_location(
                    location_name,
                    self.player
                )
            )

            add_item_rule(
                    location,
                    lambda item:
                        not item.advancement
                )


        # -------------------------------------------------
        # Mission logic
        # -------------------------------------------------

        mission_requirements = {
            "Mission - Home Repairs": [
                "Mission Unlock: Home Repairs"
            ],

            "Mission - Fruit Skewer": [
                "Mission Unlock: Fruit Skewer"
            ],

            "Mission - Magic Workshop": [
                "Mission Unlock: Magic Workshop"
            ],

            "Mission - Basement": [
                "Mission Unlock: Basement"
            ],

            "Mission - Hide and Seek": [
                "Mission Unlock: Hide and Seek",
                "Mission Unlock: Terrain Recovery"
            ],

            "Mission - Rylie's Gift": [
                "Mission Unlock: Rylie's Gift"
            ],

            "Mission - Terrain Recovery": [
                "Mission Unlock: Terrain Recovery"
            ],
        }

        for (
            location_name,
            required_items
        ) in mission_requirements.items():

            location = (
                self.multiworld.get_location(
                    location_name,
                    self.player
                )
            )

            set_rule(
                location,
                lambda state,
                        required_items=required_items:
                    all(
                        state.has(
                            item_name,
                            self.player
                        )
                        for item_name
                        in required_items
                    )
            )


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

            "sephirites_per_floor":
                self.options
                    .sephirites_per_floor
                    .value,

            "randomize_anvil_unlocks":
                bool(
                    self.options
                        .randomize_anvil_unlocks
                        .value
                ),

            "starting_cintamani_stones":
                self.options
                    .starting_cintamani_stones
                    .value,

            "sapphire_multiplier":
                self.options
                    .sapphire_multiplier
                    .value,

            "death_link":
                bool(
                    self.options
                        .death_link
                        .value
                ),

            "starting_costume":
                self.starting_costume_id,

            "starting_weapon":
                self.starting_weapon_type,

            "traps":
                bool(
                    self.options
                        .traps
                        .value
                ),
        }