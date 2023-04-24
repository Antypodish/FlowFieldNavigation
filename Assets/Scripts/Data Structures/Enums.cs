public enum ActionType : byte
{
    NormalAttack,
    Ability,
    Movement
};
public enum UnitType : byte
{
    Unit,
    Building
};
public enum InputType : byte
{
    Mouse0Up,
    Mouse0Down,
    Mouse1Up,
    Mouse1Down,
    QDown,
    QUp,
    WDown,
    WUp,
    EDown,
    EUp,
    RDown,
    RUp
};
public enum PointedObjectLayer : byte
{
    Ground,
    Clickable,
    Selectable
}