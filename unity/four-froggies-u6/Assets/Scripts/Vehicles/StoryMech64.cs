using UnityEngine;

// ffu64: bones the aircraft pads (Vehicles/Mech64.cs) follow: 0 torso, 1 head, 2 left arm, 3 right arm
public partial class StoryMech
{
    public Transform Bone64(int i) { return i == 0 ? torso : i == 1 ? head : i == 2 ? armL : armR; }
}
