# External References

This folder collects external documents that the CSW and CSWUnity requirements
draw on. The documents are copyrighted by their owners and are not stored in the
repository. Only this index is versioned. To read a document, download it from
its source and keep it outside the repository. [`.gitignore`](.gitignore)
stops PDF and HTML files here from being added by mistake.

## Vega and IRIS Performer

Vega (MultiGen-Paradigm, later Presagis, withdrawn in 2005) and IRIS Performer
(Silicon Graphics) were early real-time visual-simulation APIs. Vega was built
on Performer. Both define the viewpoint, motion models, and channels that the
CSW Observer model in
[generic/requirements.md](../requirements/generic/requirements.md) resembles.

| File name | Document | Source |
| --- | --- | --- |
| `Vega_BigBook.pdf` | Vega product overview ("BigBook") | <https://www.cs.princeton.edu/courses/archive/spring01/cs598b/papers/vega.pdf> |
| `Vega_FAQ_Part2_Tomlinson.htm` | Gordon Tomlinson's Vega FAQ, part 2 | <http://www.3dscenegraph.com/vega/vegafaq2.htm> |
| `Vega_HowTo_Tether_Observer_to_Player.htm` | How to tether a vgObserver to a vgPlayer | <http://www.vis-sim.com/vega/code/obs2player.htm> |
| `IRIS_Performer_Programmers_Guide_007-1680-030.pdf` | IRIS Performer Programmer's Guide, 007-1680-030 | <https://www.irix7.com/techpubs/007-1680-030.pdf> |
| `IRIS_Performer_Programmers_Guide_007-1680-050.pdf` | IRIS Performer Programmer's Guide, 007-1680-050 | <https://www.infania.net/misc1/sgi_techpubs/techpubs/007-1680-050.pdf> |
| `IRIS_Performer_CPP_Reference_Pages_007-2782-001.pdf` | IRIS Performer C++ Reference Pages | <https://irix7.com/techpubs/007-2782-001.pdf> |

### Vega Observer model

- **vgObserver** is the viewpoint. It is moved by a vgMotion model, by a
  vgNavigator that follows a path, by application code through `vgPosVec` and
  `vgPos`, or by attaching it to a vgPlayer (FAQ, question 77).
- **Tethering** has two settings. `VGOBS_TETHERSTATE` controls how the observer
  follows, for example `VGOBS_FIXED`, and the BigBook lists look-at tracking.
  `VGOBS_TETHERCOORD` selects the coordinate origin, for example
  `VGOBS_TPLAYER` to use the player's coordinates. `vgObservPlyr`,
  `vgObservMot`, and `vgObservNav` connect the player, the motion model, and
  the navigator (how-to page).
- **vgPlayer** is a moving entity with positioning control, motion-model
  selection, and coordinate-system selection. vgObjects and vgObservers can be
  attached to it (BigBook; FAQ, question 77).
- **Motion models:** Spin, Drive, UFO, Warp, Fly, Trackball, and Walk; the
  complex models Flight Simulator, Missile, and PickFly; Input Device Direct;
  and up to six user-defined models. Motion models have an initial and reset
  position and support ground clamping. Walk keeps a constant height above the
  terrain and uses two intersectors for terrain clamping and step detection
  (BigBook; FAQ, questions 78–87).
- **Channels** render the observer eyepoint. Channel share groups, stereo, and
  look-at observer tracking are listed with them (BigBook).
- **LADBM** (Large Area Database Management) moves a double-precision database
  origin so that it stays close to the observer and loads an Area of Interest
  around it (BigBook).
- **AudioWorks2** places sound relative to earpoints (BigBook).
- **Vega DIS-HLA** supports stealth observers, including detonation observers
  that view all detonations (BigBook).
- **Path Tool** paths can be object-relative or player-relative (BigBook).

### IRIS Performer view model

- Performer has no Observer class. A **pfChannel** carries the view.
  `pfChanView(chan, xyz, hpr)` or `pfChanViewMat(chan, mat)` sets the
  eyepoint in world coordinates. The default eyepoint looks along +Y with +Z
  up (Programmer's Guide 030, chapter 4).
- `pfChanViewOffsets(chan, xyz, hpr)` adds a translation and rotation relative
  to the eyepoint frame each frame. Typical uses are three cockpit windows at
  45°, 0°, and −45°, and pilot and copilot views from one eyepoint.
- `pfChanFOV` and `pfChanNearFar` set the frustum.
- **pfDCS** nodes are dynamic transforms for moving objects and attachment
  points.
- The perfly sample and the utility libraries provide Drive, Fly, and
  Trackball motion models and collision support (`pfuCollideSetup`).

### Comparison with CSW

| Vega or Performer | CSW concept | CSW requirement |
| --- | --- | --- |
| vgObserver | Observer, a camera carrier | REQ-CSW-CV-001, REQ-CSW-API-008 |
| vgPlayer | Player, a moving world object with a `CSW.Position` | REQ-CSW-API-013 |
| vgPlayer with `VGOBS_TPLAYER` | Reference Frame carried by a world object | REQ-CSW-CV-003, REQ-CSW-API-013 |
| vgMotion | MotionModel | REQ-CSW-CV-009, REQ-CSW-API-015 |
| UFO motion model | UFO MotionModel | REQ-CSW-CV-010 |
| Spin and Trackball motion models | Orbital MotionModel | REQ-CSW-CV-012 |
| Walk with terrain clamping and steps | Frame-dependent movement and limits | REQ-CSW-CV-004, REQ-CSW-CV-014 |
| vgIDev input device | Input | REQ-CSW-CV-009, REQ-CSW-API-014 |
| Input Device Direct | Observer input | REQ-CSW-CV-009 |
| User-defined motion models | Specialization in application layers | REQ-CSW-CV-016 |
| `pfChanViewOffsets` | Observer-mounted Cameras | REQ-CSW-CV-007 |
| vgChannel and pfChannel | View | REQ-CSW-CV-008, REQ-CSW-API-009 |
| vgWindow and pfPipeWindow | Window | REQ-CSW-API-010 |
| vgPath and Path Tool paths | Path | REQ-CSW-API-011 |
| vgNavigator and vgSplineNavigator | Navigator | REQ-CSW-API-012 |
| AudioWorks2 earpoint | Observer hearing | REQ-CSW-CV-015 |
| LADBM origin and Area of Interest | Origin rebasing and streaming around the Observer | REQ-CSW-CV-017 |

These Vega concepts have no CSW requirement yet:

- a tether state, such as fixed, look-at, or spin, set separately from the
  motion model.
