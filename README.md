Sample Project — Nearby Connections Transport for Netcode for GameObjects

This project is a demonstration sample showcasing how to use [Nearby Connections Transport for Netcode for GameObjects](https://github.com/BernatRosello/Unity-NearbyConnections-Transport) (NGO) inside a Unity project.

The project was originally derived from the sample scene used by the [Multipeer Connectivity Transport](https://github.com/realitydeslab/netcode-transport-multipeer-connectivity-sample), but has been adapted to demonstrate the functionality of Unity Nearby Connections Transport, a custom transport that enables peer-to-peer multiplayer over local wireless connections using Google Nearby Connections.

The goal of this project is to provide a minimal, easy-to-run example that developers can use to:

verify that the transport is working correctly

understand the basic setup required for NGO

test device-to-device connectivity in real environments

experiment with local multiplayer without server infrastructure

Overview

Nearby Connections is a networking API provided by Google Play Services that allows devices in close proximity to discover each other and establish direct peer-to-peer connections.

Connections may use different wireless technologies depending on device capabilities and environment:

Bluetooth

Bluetooth Low Energy

Wi-Fi Direct

Local Wi-Fi networks

The Nearby Connections Transport for Netcode for GameObjects integrates this API into Unity’s networking stack, allowing developers to build local multiplayer experiences without requiring internet connectivity or dedicated servers.

This sample project demonstrates how to configure and use that transport within a standard Netcode for GameObjects workflow.

Features Demonstrated

The sample scene included in this project demonstrates:

Automatic peer discovery

Host / Client session creation

Secure connection establishment

Real-time data exchange between peers

Once connected, players can send messages between devices using the NGO networking layer, which internally relies on Nearby Connections as the underlying transport.

This allows developers to test local multiplayer scenarios similar to LAN environments, where devices automatically detect and connect to nearby participants running the same application.

System Requirements

This sample project has been tested on the following configuration:

Unity 6 LTS

Netcode for GameObjects

Android devices with Google Play Services

Nearby Connections relies on the Android platform for its full functionality.
For this reason, Android is currently the primary supported platform for this sample.

Other platforms may run the Unity project but will not establish Nearby Connections unless a compatible backend implementation is available.

Running the Sample
1. Install dependencies (This should happen automatically)

Ensure the following packages are installed:

Netcode for GameObjects

Unity Nearby Connections Transport (included as an embbeded package in this repository)

External Dependency Manager for Unity (EDM4U)

EDM4U is required to automatically resolve Google Play Services dependencies.

2. Open the Sample Scene

Load the sample scene included in the project.

The scene contains:

A configured NetworkManager

The NearbyConnectionsTransport component

A simple UI for starting a Host or Client session

3. Build and deploy

Build the project for Android and install it on two or more devices.

Once launched:

One device starts a Host

Other devices join as Clients

Nearby Connections automatically discovers the host and establishes a connection

Once connected, the sample scene allows basic interaction between peers to verify that the transport is functioning correctly.

Project Purpose

This repository is intended primarily as a testing and demonstration environment for the transport implementation.

It was used during development to:

validate the JNI integration

test discovery and connection workflows

debug device-to-device communication

verify compatibility with Netcode for GameObjects

Because of its simplicity, it can also serve as a starting point for developers interested in building their own local multiplayer experiences using Nearby Connections.

Relation to the Original Sample

This project was initially derived from the sample scene provided in the Multipeer Connectivity Transport for Netcode for GameObjects, which implements a similar transport using Apple's Multipeer Connectivity framework.

The structure of the scene and networking workflow was adapted from that project, while replacing the underlying networking layer with the Nearby Connections backend used by this transport.

License

This project is distributed under the MIT License.
