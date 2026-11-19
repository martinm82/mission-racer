# 🪪 Physical NFC Racing License: Setup & Printing Guide

This guide explains how to create the real physical **Racing Licenses** for your boys so they can tap their cards against your phone during the **Pit Stop** ceremony!

---

## 1. What Hardware to Buy on Amazon

Search on Amazon for:
* **"NTAG215 PVC Cards"** (White standard credit card size, ~€8 to €10 for a pack of 10).
  * *Alternative for backpacks/keychains*: **"NTAG215 Epoxy Keyfobs"** (Durable, waterproof, attached to a keyring).
* *Note*: NTAG213, NTAG215, or NTAG216 all work seamlessly with iPhone (XR, 11, 16) and Android NFC.

---

## 2. Printing the Official License Artwork

The vector artwork templates are located in [`nfc-license/templates/`](file:///Users/migasiew/workspace/git/github/mission-racer/nfc-license/templates/):

* **Lightning 7 (Age 7)**:
  * Front: [`license_lightning7_front.svg`](file:///Users/migasiew/workspace/git/github/mission-racer/nfc-license/templates/license_lightning7_front.svg)
  * Back: [`license_lightning7_back.svg`](file:///Users/migasiew/workspace/git/github/mission-racer/nfc-license/templates/license_lightning7_back.svg)
* **Thunder 10 (Age 10)**:
  * Front: [`license_thunder10_front.svg`](file:///Users/migasiew/workspace/git/github/mission-racer/nfc-license/templates/license_thunder10_front.svg)
  * Back: [`license_thunder10_back.svg`](file:///Users/migasiew/workspace/git/github/mission-racer/nfc-license/templates/license_thunder10_back.svg)

### Printing Options:
1. **Easy Home Option (Sticker Paper)**:
   * Print on standard A4 self-adhesive sticker paper or photo paper.
   * Cut to credit card size (85.6 mm × 54 mm) with rounded corners.
   * Stick the front and back stickers onto the white PVC NFC card.
2. **Lanyard / ID Badge Holder**:
   * Print on normal paper, fold front/back, insert into a standard transparent ID card sleeve with a racing lanyard, and slide the NFC card inside!

---

## 3. Programming the NFC Chip (30 Seconds per Card)

You can program the physical NFC cards using any free smartphone app (like **NFC Tools** on iOS App Store or Google Play Store):

### Step-by-Step in "NFC Tools":
1. Open the **NFC Tools** app on your iPhone or Android.
2. Tap **Write** ➔ **Add a record**.
3. Select **Custom URL / URI**.
4. Enter the unique driver code:
   * For the 7yo card: `mr://license/MR-07-ROOKIE`
   * For the 10yo card: `mr://license/MR-10-PRO`
5. Tap **Write / 1 Byte**, and hold the top of your phone to the physical NFC card.
6. A green checkmark appears: **Write Complete!** 🎉

---

## 4. How the "Pit Stop" Ceremony Works

1. Dad opens **Mission Racer 3D** on his phone and switches to **"Dad's Pit Stop & Mission Board"**.
2. Child presents their physical license after finishing homework or cleaning their room.
3. Child touches their card to the top-back of Dad's phone (where the NFC antenna is located: near the camera on iPhones).
4. **Haptic Rumble + Pneumatic Air Wrench SFX** fires instantly:
   * Screen flashes: *"Card Verified: Lightning 7!"*
   * Completed mission is checked off.
   * Dad taps **"Stamp License"**.
   * Child receives **+1 Fuel Can** and **+300 Garage Coins** in real-time!
5. On the physical back of their card, Dad can stick a gold star sticker or stamp one of the 6 mission boxes (`Math`, `Reading`, `Bedroom`, etc.).
