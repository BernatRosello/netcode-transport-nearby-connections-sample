# Sample Project — Nearby Connections Transport for Netcode for GameObjects
![Nearby Connections Demo](docs/images/demo.gif)
This project is a **demonstration sample** showcasing how to use  
[Nearby Connections Transport for Netcode for GameObjects](https://github.com/BernatRosello/Unity-NearbyConnections-Transport) inside a Unity project.

The project was originally derived from the sample scene used by the  
[Multipeer Connectivity Transport](https://github.com/realitydeslab/netcode-transport-multipeer-connectivity-sample), but has been adapted to demonstrate the functionality of **Unity Nearby Connections Transport**, a custom transport that enables **peer-to-peer multiplayer over local wireless connections** using **Google Nearby Connections**.

The goal of this project is to provide a **minimal, easy-to-run example** that developers can use to:

- Verify that the transport is working correctly
- Understand the basic setup required for Netcode for GameObjects
- Test device-to-device connectivity in real environments
- Experiment with local multiplayer without server infrastructure

---

# Table of Contents

- [Overview](#overview)
- [Features Demonstrated](#features-demonstrated)
- [Screenshots](#screenshots)
- [System Requirements](#system-requirements)
- [Running the Sample](#running-the-sample)
- [Project Purpose](#project-purpose)
- [Relation to the Original Sample](#relation-to-the-original-sample)
- [License](#license)

---

# Overview

Nearby Connections is a networking API provided by **Google Play Services** that allows devices in close proximity to discover each other and establish **direct peer-to-peer connections**.

Connections may use different wireless technologies depending on device capabilities and environment:

- Bluetooth
- Bluetooth Low Energy
- Wi-Fi Direct
- Local Wi-Fi networks

The **Nearby Connections Transport for Netcode for GameObjects** integrates this API into Unity’s networking stack, allowing developers to build **local multiplayer experiences without requiring internet connectivity or dedicated servers**.

This sample project demonstrates how to configure and use that transport within a standard **Netcode for GameObjects workflow**.

---

# Features Demonstrated

The sample scene included in this project demonstrates:

- Automatic **peer discovery**
- **Host / Client** session creation
- **Secure connection establishment**
- **Real-time data exchange between peers**

Once connected, players can send messages between devices using the **NGO networking layer**, which internally relies on Nearby Connections as the underlying transport.

This allows developers to test **local multiplayer scenarios similar to LAN environments**, where devices automatically detect and connect to nearby participants running the same application.

---

# Screenshots

## Initial Screen

Placeholder for the main menu or start screen of the sample scene.

![Initial Screen](docs/images/sample_initial_screen.png)

---

## Host Session

Example of a device acting as **Host** and advertising a session.

![Host Session](docs/images/sample_host_session.png)

---

## Client Joining

Example of a device discovering and joining the host session.

![Client Session](docs/images/sample_client_join.png)

---

## Connected Peers

Example of two devices connected and exchanging data.

![Connected Peers](docs/images/sample_connected_peers.png)

---

# System Requirements

This sample project has been tested on the following configuration:

- **Unity 6 LTS**
- **Netcode for GameObjects**
- **Android devices with Google Play Services**

Nearby Connections relies on the Android platform for its full functionality.  
For this reason, **Android is currently the primary supported platform** for this sample.

> Other platforms may run the Unity project but will not establish Nearby Connections unless a compatible backend implementation is available.

---

# Running the Sample

## 1. Install dependencies *(automatic in this repository)*

Ensure the following packages are available:

- **Netcode for GameObjects**
- **Unity Nearby Connections Transport**  
  *(included as an embedded package in this repository)*
- **External Dependency Manager for Unity (EDM4U)**

EDM4U is required to automatically resolve **Google Play Services dependencies** used by the transport.

---

## 2. Open the Sample Scene

Load the sample scene included in the project.

The scene contains:

- A configured **NetworkManager**
- The **NearbyConnectionsTransport** component
- A simple UI for starting a **Host** or **Client** session

---

## 3. Build and deploy

Build the project for **Android** and install it on **two or more devices**.

Once launched:

1. One device starts a **Host**
2. Other devices join as **Clients**
3. Nearby Connections automatically discovers the host and establishes a connection

Once connected, the sample scene allows **basic interaction between peers** to verify that the transport is functioning correctly.

---

# Project Purpose

This repository is intended primarily as a **testing and demonstration environment** for the transport implementation.

It was used during development to:

- Validate the **JNI integration**
- Test **device discovery and connection workflows**
- Debug **device-to-device communication**
- Verify compatibility with **Netcode for GameObjects**

Because of its simplicity, it can also serve as a **starting point for developers** interested in building their own local multiplayer experiences using Nearby Connections.

---

# Relation to the Original Sample

This project was initially derived from the sample scene provided in the  
**Multipeer Connectivity Transport for Netcode for GameObjects**, which implements a similar transport using **Apple's Multipeer Connectivity framework**.

The structure of the scene and networking workflow was adapted from that project, while replacing the underlying networking layer with the **Nearby Connections backend** used by this transport.

---

# License

This project is distributed under the **MIT License**.
