# Requirement

We need to demonstrate that the systems are funcitonal and its usage, for this the application is created


## Live streaming of a Fleet of trucks

We have a fictional Transport company who have n number of vessels, the vessels are present in random locations and they may travel at random speed of there choice consuming st amount of fuel. There can be random job cards and the vessels compete to grab a card and serve those.

There is a control pannel for simulator where you can add vessel entries then set the minimum and maximium speed and consumption of fuel the number of cards.

There is a Live dashboard where you should be able to see the metrics of vessel and the profit/loss acquired so far.

## Stock Market Simulator
Will consider partial implementation of the project ideas that are being considered:

    Low-Latency Network Communication:
    Develop a low-latency communication system for transmitting orders and receiving market data. Explore technologies like ZeroMQ or nanomsg for building a fast and reliable messaging system. Optimize network protocols and serialization/deserialization processes to minimize communication delays.

    Risk Management System:
    Design and implement a risk management system that can monitor and control the risk associated with trading activities. Include features like position tracking, exposure analysis, and automated risk alerts. Emphasize the importance of maintaining a stable and secure trading environment.

    High-Performance Order Matching Engine:
    Develop a high-performance order matching engine that can handle a large number of orders per second with minimal latency. Implement different order types, such as market orders and limit orders. Optimize the code for speed and efficiency, and use techniques such as multithreading and asynchronous programming.

    Algorithmic Trading Strategies:
    Create and implement a set of algorithmic trading strategies. Showcase your ability to analyze market data, make informed trading decisions, and execute orders with low latency. Experiment with different trading algorithms and backtest their performance against historical data.

    Machine Learning for Predictive Analytics:
    Explore the application of machine learning algorithms for predicting market trends or price movements. Train models on historical data and demonstrate their effectiveness in making accurate predictions. Integrate the machine learning models into your trading system.

    Market Data Feed Handler:
    Build a market data feed handler that can efficiently consume and process real-time market data from various sources. Optimize the data parsing and storage to minimize latency. Implement features like data compression and validation to ensure data integrity.

    Simulation Environment:
    Build a realistic simulation environment for testing trading strategies in a controlled setting. Include features for simulating market conditions, order execution, and transaction costs. Provide tools for performance analysis and optimization of trading algorithms.

    Infrastructure Monitoring and Optimization:
    Develop a system for monitoring the performance of the trading infrastructure in real-time. Include metrics such as CPU usage, memory usage, and network latency. Implement automated alerts and optimization strategies to ensure the system operates at peak efficiency.

Remember to document your project thoroughly, including design decisions, implementation details, and performance metrics. Additionally, consider creating a visually appealing and user-friendly interface if applicable. This will not only showcase your technical skills but also your ability to communicate complex concepts effectively.

## Stack

- React for responsive and simple ui
- SSE/websockets for update streaming 
- C# based Rest Endpoint for Actor communication
- c# based Simulator.
- Mongodb for state persistance
- RabbitMq for Backplane