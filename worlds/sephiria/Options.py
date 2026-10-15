from dataclasses import dataclass

from Options import Choice, Range, Toggle, OptionList, PerGameCommonOptions


class GoalChapter(Choice):
    """
    The chapter that must be completed to finish the Archipelago goal.
    """

    display_name = "Goal Chapter"

    option_1 = 1
    option_2 = 2
    option_3 = 3
    option_4 = 4
    option_5 = 5
    option_6 = 6

    default = 6


class RequiredChapterClears(Range):
    """
    The number of times the selected goal chapter must be completed.
    """

    display_name = "Required Chapter Clears"

    range_start = 1
    range_end = 6
    default = 1


class UniqueWeaponsRequired(Toggle):
    """
    If enabled, each required chapter clear must use a different weapon.
    """

    display_name = "Require Unique Weapons"

    default = 0


class SephiritesPerFloor(Range):
    """
    Sets the number of randomized Sephirite checks generated
    on each floor.

    Qliphoth's Temple receives half of the selected amount.
    (Applies only for Chapter 6.)

    The allowed range is 8 to 30.

    The minimum required value depends on the selected
    Goal Chapter and other item-pool options.

    Changing the list of hard mode options enabled will
    also affect the allowed range of values

    Generation will fail with an explanatory error if the
    selected value does not provide enough locations for
    all generated items.

    Recommended values that work with all current
    item-pool settings:
    Chapter 1: 30
    Chapter 2: 21
    Chapters 3-6: 16
    """

    display_name = "Sephirites Per Floor"

    range_start = 8
    range_end = 30
    default = 16


class RandomizeAnvilUnlocks(Toggle):
    """
    If enabled, the two Progressive Anvil Unlocks are added to the
    Archipelago item pool.

    If disabled, both Anvil upgrades are available from the beginning.
    """

    display_name = "Randomize Anvil Unlocks"

    default = 1


class StartingCintamaniStones(Range):
    """
    Adds Starting Cintamani Stone unlocks to the
    Archipelago item pool.

    Each received unlock grants one Cintamani Stone
    charge at the beginning of every future run.

    If two charges are unlocked, the second Stone is
    granted after the first is consumed.

    A Cintamani Stone breaks on fatal damage, restores
    50% HP, and grants brief invincibility.

    Maximum: 2
    """

    display_name = "Starting Cintamani Stones"

    range_start = 0
    range_end = 2
    default = 0


class Traps(Toggle):
    """
    Adds trap items to the Archipelago item pool.

    Four copies of each of the five Sephiria traps are added,
    for a total of 20 trap items.

    Timed traps last between 20 and 30 seconds.
    Bag Shuffle Trap activates instantly.
    """

    display_name = "Traps"

    default = 0


class SapphireMultiplier(Range):
    """
    Multiplies Sapphire earned through normal Sephiria gameplay.

    This does not multiply Sapphire received from Archipelago filler items.
    """

    display_name = "Sapphire Multiplier"

    range_start = 1
    range_end = 10
    default = 1


class DeathLink(Toggle):
    """
    Enables DeathLink during active Sephiria runs.

    DeathLinks received while in the overworld are discarded.

    DeathLinks received during an active run are applied to the player.
    If a DeathLink is received during a cutscene or another unsafe moment,
    it will be delayed until the player can safely receive it.
    """

    display_name = "Death Link"

    default = 0


class EnabledHardModeSettings(OptionList):
    """
    Determines which Hard Mode modifiers are enabled
    in the Archipelago seed.

    All modifiers are enabled by default.

    Remove a modifier from the YAML list to disable it.

    Enabled modifiers start at maximum difficulty
    and can be reduced through Archipelago items.

    Disabled modifiers start at level 0 and their
    reduction items are excluded from the item pool.
    """

    display_name = "Enabled Hard Mode Settings"

    valid_keys = {
        "Ferocious Claws",
        "Tenacious Body",
        "Protective Armor",
        "Agile Claws",
        "Evil Energy",
        "Combat Stance",
        "Festival of Blood",
        "False Vanguard",
        "Dispossession",
        "Dizziness",
        "Deterioration",
        "Tension",
        "Heavy Heart",
        "Decline",
        "Severed Exchange",
        "Rebellion of Machines",
        "Proliferation",
    }

    default = [
        "Ferocious Claws",
        "Tenacious Body",
        "Protective Armor",
        "Agile Claws",
        "Evil Energy",
        "Combat Stance",
        "Festival of Blood",
        "False Vanguard",
        "Dispossession",
        "Dizziness",
        "Deterioration",
        "Tension",
        "Heavy Heart",
        "Decline",
        "Severed Exchange",
        "Rebellion of Machines",
        "Proliferation",
    ]


@dataclass
class SephiriaOptions(PerGameCommonOptions):
    goal_chapter: GoalChapter
    required_chapter_clears: RequiredChapterClears
    unique_weapons_required: UniqueWeaponsRequired

    sephirites_per_floor: SephiritesPerFloor
    randomize_anvil_unlocks: RandomizeAnvilUnlocks
    starting_cintamani_stones: StartingCintamaniStones
    traps: Traps
    sapphire_multiplier: SapphireMultiplier
    death_link: DeathLink
    enabled_hard_mode_settings: EnabledHardModeSettings