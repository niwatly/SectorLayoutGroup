# SectorLayoutGroup

SectorLayoutGroup allows you to align children along the custom sector shape in Unity.


<img src="https://github.com/niwatly/SectorLayoutGroup/blob/master/images/eyecatch.png" width="300px">


You can define a sector shape in 3D, and adjust the following parameters by game object positioning.

1. *Center* Position
2. *Start* Position
3. *End* Position


<img src="https://github.com/niwatly/SectorLayoutGroup/blob/master/images/howtouse.png" width="300px">



If you don't need the sector but circle, [uGUI-Circle-Layout-Group](https://github.com/hont127/uGUI-Circle-Layout-Group) is more simply.

## Demos

<img src="https://github.com/niwatly/SectorLayoutGroup/blob/master/images/demo2.gif">

<img src="https://github.com/niwatly/SectorLayoutGroup/blob/master/images/demo1.gif">

## Requirement

[UniRx](https://github.com/neuecc/UniRx)

## Usage

1. Import the [SectorLayoutGroup.cs](https://github.com/niwatly/SectorLayoutGroup/blob/master/SectorLayoutGroup.cs) script to your project.
2. Create a new game object and attach the SectorLayoutGroup.
3. Create the new game object inside the SectorLayoutGroup game object, and attach to the SectorLayoutGroup parameter *Center*.
4. In the same way, create a new game object as the *Start*.
5. In the same way, create a new game object as the *End*.

These steps are packed in the [SelctorLayoutGroup.prefab](https://github.com/niwatly/SectorLayoutGroup/blob/master/SectorLayoutPrefab.prefab)!

## Custom Parameters

<img src="https://github.com/niwatly/SectorLayoutGroup/blob/master/images/inspector.png" width="300px">

### child rotation offset

You can adjust where chilren look at. By default, children look at the *Center*.

### Animation Settings

When a child has beeen added to SectorLayoutGroup game object in play mode, children moves new position smootly.

you can adjust the moving speed and duraton by editing the *FrameCount* and *FrameInterval* parameters, 

## Author

[Niwatly](https://github.com/niwatly)
