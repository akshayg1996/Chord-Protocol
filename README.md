# Chord Protocol
The main aim of this project is to build an Actor-model based simulator using AKKA framework to implement the chord protocol and a simple object access service to prove its usefulness. 

# Team Members
1. Akshay Ganapathy (UFID - 3684-6922)
2. Kamal Sai Raj Kuncha (UFID - 4854-8114)

# Problem definition
Build an Actor-model based simulator using AKKA framework to implement the chord protocol and a simple object access service to prove its usefulness.

# Requirements
Input: The input to the program is the number of nodes (which is the number of actors involved), and the number of requests (number of requests executed at each node).

Output: The output is the average number of hops that have been traversed for finding the requested key (deliver the message).

# How To Run
Run the project3.fsx file using the command: 
“dotnet fsi project3.fsx <numNodes> <numRequests>”

where 	‘numNodes’ is the number of nodes used, and
‘numRequests is the number of requests executed at each node.

# Languages used

F# was used to code the project.

# Platforms used for running the code
Visual Studio Code
.NET version 5.0
NuGET Akka.NET v1.4.25

# What is working?

• Each  node  is  added  to  the  network  consecutively  and  after  every  node  has  joined  the  network,  the  message  starts  getting transmitted  to  every other node.
  
• The transmission is terminated after each node gets a request.
  
• The average number of hops for the test cases ranged between 1.2 to 5.846. We can infer from this statistic that while searching in a node, the average number of hops would be about 3.
  
• Given, the number of requests to be delivered to every node using the chord protocol, the average number of hops (output) has been generated and implemented successfully.
  
• All functionalities have been implemented and are working successfully.

# Largest network obtained

The largest network we were able to work with was 5000 nodes and 10 requests. The average number of hops was found to be 5.846. 

# Some Interesting Observations 
• When the number of requests is greater than the number of nodes, we observed a slight increase (less than 1) in the average hope count.
  
• Larger the number of nodes in the network and smaller the number of requests, we observed a reasonable increase (greater than 1) in the average number of hops. The average hop values in this case were observed to lie between 2 and 5. 
  
• When the number of requests and the number of nodes are equal (difference is negligible), the average number of hops are less than or equal to 1. 
  
• Larger the number of nodes in the network, the program takes a substantial amount of time to finish execution.

