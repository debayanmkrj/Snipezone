import cv2
import mediapipe as mp
import numpy as np
import socket
import json
import time

class FaceTracker:
    def __init__(self):
        self.mp_face_mesh = mp.solutions.face_mesh
        self.face_mesh = self.mp_face_mesh.FaceMesh(
            max_num_faces=1,
            refine_landmarks=True,
            min_detection_confidence=0.5,
            min_tracking_confidence=0.5
        )
        
        # Eye indices for blink detection
        self.LEFT_EYE_TOP = 159
        self.LEFT_EYE_BOTTOM = 145
        self.RIGHT_EYE_TOP = 386
        self.RIGHT_EYE_BOTTOM = 374
        
        # Blink detection
        self.blink_counter = 0
        self.is_blinking = False
        self.blink_sequence = []
        self.last_blink_time = 0
        
        # Socket setup
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.unity_address = ('localhost', 12345)
        
        # Calibration
        self.center_x = 0
        self.center_y = 0
        self.calibrated = False
        
    def get_eye_distance(self, landmarks, top_idx, bottom_idx):
        """Get vertical distance between eye landmarks"""
        top = landmarks[top_idx]
        bottom = landmarks[bottom_idx]
        distance = abs(top.y - bottom.y)
        return distance
    
    def detect_blink_simple(self, landmarks):
        """Simple blink detection"""
        # Get eye distances
        left_distance = self.get_eye_distance(landmarks, self.LEFT_EYE_TOP, self.LEFT_EYE_BOTTOM)
        right_distance = self.get_eye_distance(landmarks, self.RIGHT_EYE_TOP, self.RIGHT_EYE_BOTTOM)
        
        avg_distance = (left_distance + right_distance) / 2.0
        
        # Print for debugging
        print(f"Eye distance: {avg_distance:.4f}")
        
        current_time = time.time()
        
        # Detect if eyes are closed (threshold needs adjustment)
        eye_closed = avg_distance < 0.01  # Adjust this value
        
        if eye_closed and not self.is_blinking:
            self.is_blinking = True
            self.blink_sequence.append(current_time)
            print(f"BLINK! Total blinks: {len(self.blink_sequence)}")
            
        elif not eye_closed and self.is_blinking:
            self.is_blinking = False
            
        # Clean old blinks (older than 3 seconds)
        self.blink_sequence = [t for t in self.blink_sequence if current_time - t < 3.0]
        
        # Check for 3 blinks
        if len(self.blink_sequence) >= 3:
            print("THREE BLINKS DETECTED - SHOOTING!")
            self.blink_sequence.clear()  # Reset
            return True
            
        return False
    
    def calibrate(self, landmarks):
        nose_tip = landmarks[1]
        self.center_x = nose_tip.x
        self.center_y = nose_tip.y
        self.calibrated = True
        print("Calibration complete!")
    
    def get_head_rotation(self, landmarks):
        if not self.calibrated:
            return 0, 0
        
        nose_tip = landmarks[1]
        delta_x = (nose_tip.x - self.center_x) * 100
        delta_y = (nose_tip.y - self.center_y) * 100
        
        return delta_x, -delta_y
    
    def send_to_unity(self, rotation_x, rotation_y, shoot):
        data = {
            'rotationX': rotation_x,
            'rotationY': rotation_y,
            'shoot': shoot
        }
        
        message = json.dumps(data).encode('utf-8')
        try:
            self.sock.sendto(message, self.unity_address)
        except:
            pass
    
    def run(self):
        cap = cv2.VideoCapture(0)
        
        print("Face tracker started!")
        print("Press 'c' to calibrate")
        print("Press 's' to test manual shooting")
        print("Press 'q' to quit")
        
        while cap.isOpened():
            success, image = cap.read()
            if not success:
                continue
            
            image = cv2.flip(image, 1)
            image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
            results = self.face_mesh.process(image_rgb)
            
            rotation_x, rotation_y = 0, 0
            shoot = False
            
            if results.multi_face_landmarks:
                for face_landmarks in results.multi_face_landmarks:
                    landmarks = face_landmarks.landmark
                    
                    rotation_x, rotation_y = self.get_head_rotation(landmarks)
                    shoot = self.detect_blink_simple(landmarks)
                    
                    # Draw reference points
                    h, w, _ = image.shape
                    nose_tip = landmarks[1]
                    x = int(nose_tip.x * w)
                    y = int(nose_tip.y * h)
                    cv2.circle(image, (x, y), 5, (0, 255, 0), -1)
            
            self.send_to_unity(rotation_x, rotation_y, shoot)
            
            # Display info
            cv2.putText(image, f"Blinks: {len(self.blink_sequence)}/3", (10, 30), 
                       cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
            
            if not self.calibrated:
                cv2.putText(image, "Press 'c' to calibrate", (10, 60), 
                           cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
            
            cv2.imshow('Face Tracker', image)
            
            key = cv2.waitKey(1) & 0xFF
            if key == ord('q'):
                break
            elif key == ord('c') and results.multi_face_landmarks:
                for face_landmarks in results.multi_face_landmarks:
                    self.calibrate(face_landmarks.landmark)
                    break
            elif key == ord('s'):  # Manual test
                print("Manual shoot test!")
                self.send_to_unity(0, 0, True)
        
        cap.release()
        cv2.destroyAllWindows()
        self.sock.close()

if __name__ == "__main__":
    tracker = FaceTracker()
    tracker.run()