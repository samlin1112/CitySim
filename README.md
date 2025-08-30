# SimCity-like Game

## Overview

This is a city simulation game where players can construct residential, commercial, and industrial zones, build roads and public facilities, manage city resources, and observe urban development and population growth. The game emphasizes strategic planning and resource balancing and is fully open-source on GitHub.

Beyond gameplay, this project serves as a programming and system design experiment, combining event-driven programming, data structure management, and simple AI to simulate dynamic city behavior and interaction.

---

## Features

* **City Planning & Construction**: Freely build residential, commercial, and industrial areas,.
* **Resource Management**: Simulate population, finance, pollution, and traffic indicators, requiring players to balance city development.
* **Dynamic Events**: Random events such as quakes affect city operations, adding strategic challenges.
* **Interactive Feedback**: Real-time charts and indicators provide insight into city status for better decision-making.

---

## Installation & Execution

1. Clone the repository
2. Open the project in Visual Studio (C# / WinForms).
3. Build and run.

---

## Controls

* **Build**: Select a building type and click on the map to place.
* **View City Info**: Check population, budget, pollution, and traffic indicators on the right panel.
* **Pause/Reset**: Press the button on the right panel.

---

## Technical Architecture

### Core Components

* `GameForm`: Handles UI rendering, event-driven input, and game state management.
* `CityMap`: Stores each grid cell’s building type and status, providing query and update methods.
* `Building` Classes: Define properties and behavior of Residential, Commercial, power, water, and Industrial.
* `SimulationEngine`: Manages population growth, resource flow, event triggers, and indicator calculations.

### Key Technologies

* **Event-Driven Programming**: Handle player input through WinForms events (mouse and keyboard).
* **Data Structure Management**: Use 2D arrays or lists for map grids, dictionaries for building statistics.
* **Simple AI Simulation**: Use formulas and random events to simulate population growth, tax expenditures, and water and electricity demand.
* **Performance Optimization**: DoubleBuffered drawing reduces flickering; only updated grid areas are redrawn for efficiency.

---

## Future Improvements

* Implement more detailed economic and social simulations (tax, employment, education, public services).
* Add building upgrade system and advanced city event AI.
* Port to WPF or Unity for better graphics and performance.
* Implement save/load and multiplayer collaboration features.

