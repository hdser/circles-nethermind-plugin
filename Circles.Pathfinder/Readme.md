# Pathfinding Process Overview

This summary outlines the steps taken to calculate and return a path from a request to a response in the Circles Pathfinder system.

## Steps to Compute Max Flow and Return Paths

1. **Receive Flow Request**

    - Input: `FlowRequest` containing:
        - `Source`: The starting node (avatar address).
        - `Sink`: The target node (avatar address).
        - `TargetFlow`: The desired amount of flow to transfer.

2. **Validate Request Parameters**

    - Ensure `Source` and `Sink` are provided and not empty.
    - Parse `TargetFlow` into a numerical value (e.g., `BigInteger`).
    - Confirm that `Source` and `Sink` are valid nodes (not balance nodes).

3. **Load Graph Data**

    - **Trust Graph**: Load trust relationships between users from the database.
        - Each `TrustEdge` represents a trust relationship from one user to another.
    - **Balance Graph**: Load balances of tokens held by users.
        - Each `BalanceNode` represents a user's balance for a specific token.
        - Each balance is connected to the user's `AvatarNode` via a `CapacityEdge`.

4. **Create Capacity Graph**

    - Combine the **Trust Graph** and **Balance Graph** into a **Capacity Graph**.
        - Nodes:
            - All `AvatarNode` and `BalanceNode` instances from both graphs.
        - Edges:
            - Retain capacity edges from the balance graph.
            - For each balance, add capacity edges to all avatars that trust the token owner (from balance node to trusting avatars).
        - Purpose: Represents potential capacities for token transfers based on trust and available balances.

5. **Create Flow Graph**

    - Convert the **Capacity Graph** into a **Flow Graph**.
        - Replace `CapacityEdge` instances with `FlowEdge` instances.
        - Initialize `CurrentCapacity` and `Flow` for each edge.
        - Purpose: Used for computing actual flows using flow algorithms.

6. **Compute Maximum Flow**

    - Use the **Edmonds-Karp algorithm** (BFS-based) to find augmenting paths and compute the maximum possible flow up to the `TargetFlow`.
        - Method: `ComputeMaxFlowWithPaths(source, sink, targetFlow)`
        - Steps:
            - While there is a path from `Source` to `Sink` with available capacity:
                - Find the shortest augmenting path using BFS.
                - Determine the minimum capacity (`pathFlow`) along this path.
                - Update the flow and capacities along the path.
                - Accumulate the total flow (`maxFlow`).
                - Stop when `maxFlow` reaches `TargetFlow`.

7. **Extract Paths with Positive Flow**

    - After computing the max flow, extract all paths that have contributed to the flow.
        - Method: `ExtractPathsWithFlow(source, sink, threshold)`
        - Steps:
            - Perform DFS from `Source` to `Sink`, following edges with positive flow.
            - Collect all unique paths where the flow is above a specified threshold.
        - Purpose: Identify the actual paths through which tokens will be transferred.

8. **Collapse Balance Nodes**

    - Simplify the paths by collapsing balance nodes, focusing on transfers between avatars.
        - Merge consecutive edges that pass through a balance node into a single edge.
        - Adjust the flow values accordingly.
        - Result: A simplified graph where paths represent direct transfers between users.

9. **Generate Transfer Steps**

    - Convert the collapsed paths into a list of transfer steps.
        - For each edge in the collapsed graph:
            - Create a `TransferPathStep` containing:
                - `From`: Sender's avatar address.
                - `To`: Receiver's avatar address.
                - `TokenOwner`: Owner of the token being transferred.
                - `Value`: Amount of tokens to transfer.
        - Purpose: Provides the sequence of transfers needed to achieve the max flow.

10. **Prepare and Return Response**

    - Create a `MaxFlowResponse` containing:
        - `MaxFlow`: The total amount of tokens that can be transferred (as a string).
        - `Transfers`: The list of `TransferPathStep` instances representing the transfer paths.
    - Return the response to the requester.

## Key Components Involved

- **Nodes**:
    - `AvatarNode`: Represents a user/account.
    - `BalanceNode`: Represents a user's balance for a specific token.

- **Edges**:
    - `TrustEdge`: Represents trust from one user to another.
    - `CapacityEdge`: Potential capacity for token transfer.
    - `FlowEdge`: Actual flow of tokens during pathfinding.

- **Graphs**:
    - `TrustGraph`: Contains all trust relationships.
    - `BalanceGraph`: Contains all balances.
    - `CapacityGraph`: Combines trust and balances to represent transfer capacities.
    - `FlowGraph`: Used for computing actual flows.