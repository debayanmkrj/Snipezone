# SnipeZone - Tactical FPS with Face Tracking
![Screenshot 2025-12-29 190016](https://github.com/user-attachments/assets/4d50dd63-e2b4-4c86-a47c-aef306e076ee)



**A precision First-Person Shooter (FPS) sniping game featuring innovative face tracking controls powered by MediaPipe and intelligent enemy AI.**

---

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [Gameplay](#gameplay)
- [Technical Architecture](#technical-architecture)
- [Installation & Setup](#installation--setup)
- [Controls](#controls)
- [Project Structure](#project-structure)
- [Technologies Used](#technologies-used)
- [Future Enhancements](#future-enhancements)
- [Credits](#credits)

---

## Overview

SnipeZone is an immersive tactical FPS sniping experience developed for the AME 598 - Animating Virtual Worlds course at Arizona State University. The game combines traditional FPS mechanics with cutting-edge computer vision technology, allowing players to control camera movement and shooting through facial movements and blink detection.

Players take on the role of a sniper positioned 2-3 stories above street level in an industrial urban environment, facing waves of intelligent enemy combatants with advanced AI behaviors including flanking, cover seeking, and tactical repositioning.

---

## Key Features

### Dual Control Modes
- **Traditional Controls**: Classic keyboard + mouse input for movement, aiming, and shooting
- **Face Tracking Mode**: Hands-free gameplay using:
  - Head movement for camera control
  - Triple-blink detection for shooting
  - Real-time face landmark tracking via MediaPipe

### Advanced Enemy AI
Enemies feature a sophisticated state machine with 8 behavioral states:
- **Idle & Wander**: Patrol behavior when no threat detected
- **Seek**: Actively search for player position
- **Fire**: Engage player with distance-based accuracy
- **Taking Cover**: Seek protection when health is low
- **Flanking**: Attempt tactical repositioning (30% chance)
- **Investigating**: Check noise sources
- **Dying**: Death animation state

### Combat Systems
- **Player Health System**: 100 HP with visual health bar
- **Scope System**: Dual-camera setup with zoom functionality
- **Weapon Mechanics**:
  - Fire rate control (0.8 shots/second)
  - Ammo management (20 rounds, manual reload)
  - Muzzle flash and audio effects
- **Hit Detection**: Raycast-based with near-miss detection

### Enemy Intelligence
- **Accuracy System**: Distance-based falloff with movement penalties
- **Cover System**: Tactical scoring to find optimal cover points
- **Repositioning Logic**: Adapts after consecutive misses or blocked shots
- **Hearing System**: Responds to gunfire noise within 40 units
- **Line of Sight (LOS)**: Optimized raycast checks every 0.1 seconds

---

## Gameplay

The player assumes a defensive sniper position with:
- **Initial Health**: 100 HP
- **Weapon**: Sniper rifle with scope
- **Objective**: Survive waves of approaching enemies
- **Strategic Position**: Elevated vantage point for tactical advantage

**Enemy Behavior**:
- Assault enemies approach from the front (excluding player's blind spot)
- Sniper enemies maintain distance while firing
- Both types use cover, flanking, and tactical repositioning
- Accuracy decreases with distance and while moving

---

## Technical Architecture

### Unity Systems

#### Player Scripts
Located in `Prototype/Snipezone_1/Assets/Scripts/Player/`

**PlayerMovement.cs** (`Prototype\Snipezone_1\Assets\Scripts\Player\PlayerMovement.cs:1`)
- First-person character controller
- Velocity-based movement system
- Ground detection with optimized physics checks (every 0.1s)
- Dual footstep audio sources for stereo effect
- Health management and damage handling

**CameraController.cs** (`Prototype\Snipezone_1\Assets\Scripts\Player\CameraController.cs:1`)
- Mouse-look camera system
- Seamless switching between mouse and face tracking input
- Rotation clamping (X: -30° to 30°, Y: -360° to 360°)
- Quaternion Slerp for smooth rotation
- Cursor lock management based on input mode

**FaceTrackingIntegration.cs** (`Prototype\Snipezone_1\Assets\Scripts\Player\FaceTrackingIntegration.cs:1`)
- UDP listener on port 12345
- Background thread for non-blocking packet reception
- Thread-safe data exchange using primitive types
- Smoothed input with Lerp interpolation
- Shooting trigger from blink detection

#### Shooting Scripts
Located in `Prototype/Snipezone_1/Assets/Scripts/Shooting/`

**ShootingController.cs** (`Prototype\Snipezone_1\Assets\Scripts\Shooting\ShootingController.cs:1`)
- Weapon firing with configurable fire rate
- Ammo tracking and reload mechanics
- Raycast hit detection with hierarchy checks
- Near-miss sphere cast detection (2-unit radius)
- Enemy alert system for noise propagation
- Face tracking integration via `TriggerFaceShoot()`

**ScopeSystem.cs** (`Prototype\Snipezone_1\Assets\Scripts\Shooting\ScopeSytem.cs:1`)
- Dual-camera system (scoped/unscoped)
- Dynamic FOV adjustment with scroll wheel
- Auto-unscope during shooting and reloading
- Visual scope overlay toggle

#### Enemy AI Scripts
Located in `Prototype/Snipezone_1/Assets/Scripts/Enemy/`

**MoveEnemyAssault.cs** (`Prototype\Snipezone_1\Assets\Scripts\Enemy\MoveEnemyAssault.cs:1`) - 1,372 lines
- 8-state FSM (Finite State Machine)
- NavMesh-based pathfinding with manual position sync
- Accuracy calculation: `baseAccuracy - movingPenalty - (distance × falloff)`
- Spread angle: `maxSpreadAngle × (1 - accuracy)`
- Cover scoring system prioritizing distance and LOS
- Flanking calculation using perpendicular vectors
- Animation blend tree with velocity mapping
- Audio system for footsteps, gunfire, and reactions

**MoveEnemySnipper.cs**
- Similar structure to Assault variant
- Longer optimal attack distance (80 units vs 50)
- Same fire rate but different engagement tactics

### Python Face Tracking System

Located in `Prototype/FaceTracking/`

**face_tracker.py** (`Prototype\FaceTracking\face_tracker.py:1`)
- MediaPipe Face Mesh integration
- Real-time landmark detection (468 points)
- Eye tracking indices:
  - Left eye: Top (159), Bottom (145)
  - Right eye: Top (386), Bottom (374)
- Blink detection algorithm:
  - Vertical eye distance threshold: < 0.01
  - Triple-blink sequence within 3 seconds triggers shoot
- Head rotation tracking via nose tip (landmark 1)
- UDP transmission at localhost:12345
- Calibration system for centered position

**requirements.txt** (`Prototype\FaceTracking\requirements.txt:1`)
```
mediapipe==0.10.7
opencv-python==4.8.1.78
numpy==1.26.4
```

### Communication Protocol

**UDP Data Format** (JSON):
```json
{
  "rotationX": float,  // Horizontal head rotation
  "rotationY": float,  // Vertical head rotation
  "shoot": bool        // Triple blink detected
}
```

**Data Flow**:
1. Python captures webcam feed
2. MediaPipe processes facial landmarks
3. Calculate head rotation delta from calibrated center
4. Detect blink sequences
5. Send JSON packet via UDP to Unity
6. Unity background thread receives packet
7. Main thread smooths input with Lerp
8. Camera controller applies rotation
9. Shooting controller executes fire command

---

## Installation & Setup

### Prerequisites
- **Unity**: 2021.3 LTS or later
- **Python**: 3.8+
- **Webcam**: For face tracking mode

### Unity Setup

1. Clone or download this repository
2. Open Unity Hub
3. Click "Add" and navigate to: `Prototype/Snipezone_1/`
4. Select Unity version 2021.3 or later
5. Open the project

### Face Tracking Setup

1. Navigate to the FaceTracking directory:
   ```bash
   cd "Prototype/FaceTracking"
   ```

2. Create a Python virtual environment:
   ```bash
   python -m venv venv
   ```

3. Activate the virtual environment:
   - **Windows**: `venv\Scripts\activate`
   - **macOS/Linux**: `source venv/bin/activate`

4. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```

5. Run the face tracker:
   ```bash
   python face_tracker.py
   ```

6. Press 'c' to calibrate your center position
7. Triple-blink to test shooting trigger
8. Press 'q' to quit

### Running the Game

1. Launch Unity and open the main scene
2. (Optional) Start `face_tracker.py` if using face tracking
3. Press Play in Unity Editor
4. Toggle face tracking in the FaceTrackingIntegration component
5. Use traditional controls or face tracking to play

---

## Controls

### Traditional Mode (Default)

| Action | Control |
|--------|---------|
| Move Forward/Back | W/S |
| Strafe Left/Right | A/D |
| Look Around | Mouse Movement |
| Aim (Scope) | Right Mouse Button |
| Shoot | Left Mouse Button |
| Zoom In/Out (Scoped) | Mouse Scroll Wheel |
| Reload | R |

### Face Tracking Mode

| Action | Control |
|--------|---------|
| Move Forward/Back | W/S (keyboard) |
| Strafe Left/Right | A/D (keyboard) |
| Look Around | Head Movement |
| Shoot | Triple Blink (3 quick blinks) |
| Calibrate | Press 'C' in Python window |

**Note**: Face tracking mode requires the Python script to be running. Movement remains keyboard-controlled for stability.



## Technologies Used

### Unity
- **Version**: 2021.3 LTS
- **Character Controller**: Built-in CharacterController component
- **NavMesh**: AI pathfinding system
- **Animator**: Blend trees for character animations
- **Audio**: AudioSource components for spatial sound
- **UI**: Canvas-based health bars

### Python Libraries
- **MediaPipe 0.10.7**: Face mesh landmark detection
- **OpenCV 4.8.1.78**: Webcam capture and image processing
- **NumPy 1.26.4**: Array operations for landmark data
- **Socket**: UDP communication with Unity

### Assets & Tools
- **Industrial Set**: Environment assets from Unity Asset Store
- **WarFX**: Muzzle flash and combat particle effects
- **Ilumisoft Health System**: Health bar UI components
- **Custom Crosshairs**: Scope overlay graphics

### Development
- **IDE**: Visual Studio / Rider for C# scripting
- **Version Control**: Git (recommended)
- **Documentation**: PDF technical documentation included

---

## System Details

### Performance Optimizations
- Ground check reduced to 0.1s intervals instead of every frame
- LOS checks optimized with 0.1s polling
- NavMesh agent position sync to prevent desync stuttering
- Animation blend tree deadzone (< 0.05) to reduce jitter


