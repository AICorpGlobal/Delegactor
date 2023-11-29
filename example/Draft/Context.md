# Requirement

We need to demonstrate that the systems are funcitonal and its usage, for this the application is created


## Live streaming of a Fleet of trucks

We have a fictional Transport company who have n number of vessels, the vessels are present in random locations and they may travel at random speed of there choice consuming st amount of fuel. There can be random job cards and the vessels compete to grab a card and serve those.

There is a control pannel for simulator where you can add vessel entries then set the minimum and maximium speed and consumption of fuel the number of cards.

There is a Live dashboard where you should be able to see the metrics of vessel and the profit/loss acquired so far.

## Stock Market Simulator

We have a fictional stock market auto trader, in which a number of stocks have to be purchased when the price is low and sold when the price is high or a preset time hits where a random purchase or sell had to be triggered

There is a control pannel for simulator where you can add stock entries then set the minimum and maximium when the selling has to be fired

There is a Live dashboard where you should be able to see the current values and the profit or loss have to be calcluated.

## Stack

- React for responsive and simple ui
- SSE/websockets for update streaming 
- C# based Rest Endpoint for Actor communication
- c# based Simulator.
- Mongodb for state persistance
- RabbitMq for Backplane