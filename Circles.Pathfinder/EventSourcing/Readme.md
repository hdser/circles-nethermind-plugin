# TrustGraphAggregator

## Purpose

- **Maintain the Trust Graph**: Keeps an accurate and current graph of all active trust relationships between users (avatars) in the network.
- **Process Events**: Ingests events related to trust actions and blockchain advancements to update the graph accordingly.
- **Handle Trust Expiry**: Automatically removes expired trust relationships based on the current blockchain timestamp.
- **Support Reversion**: Allows reverting the state of the trust graph to a previous block number and timestamp, facilitating features like reorg handling.

## How It Works

### Core Components

- **Aggregator Base Class**: The `TrustGraphAggregator` leverages a generic `Aggregator<TEvent, TState>` class that applies events to a state using reversible actions.
- **TrustGraph**: The state maintained by the aggregator, representing the network's trust relationships.
- **Event Actions**: Actions derived from events that modify the `TrustGraph`. Each action can be applied or reversed.

### Event Processing

The aggregator processes two main types of events:

1. **Trust Events** (`Trust`): Represent actions where a user (truster) establishes or revokes trust with another user (trustee).
2. **Block Events** (`BlockEvent`): Indicate the progression of the blockchain, primarily used to trigger the expiry of trust relationships.

#### Processing Trust Events

- **Adding Trust**: When a `Trust` event with a future expiry time is received, an `AddTrustAction` is created and applied to the `TrustGraph`, adding a trust edge between the truster and trustee.
- **Removing Trust**: If the trust has already expired at the event's timestamp, a `RemoveTrustAction` is created instead, ensuring that the trust relationship is not added to the graph.

#### Processing Block Events

- **Updating Timestamp**: Advances the aggregator's internal timestamp to the block's timestamp.
- **Expiring Trusts**: Checks all trust relationships in the `TrustGraph` and removes those whose expiry times are less than or equal to the current timestamp. This is done by creating and applying `RemoveTrustAction` instances for each expired trust.

### Timestamp Management

- **Monotonic Increase**: The aggregator maintains an internal `_currentTimestamp` that only moves forward. Processing an event with an older timestamp than the current one raises an exception, ensuring the integrity of the event sequence.
- **Event Ordering**: Requires that events are processed in chronological order to maintain consistency.

### Reverting State

- **RevertToBlock Method**: Allows the aggregator to revert the `TrustGraph` to a specific block number and timestamp.
    - **Reversing Actions**: Applies the inverse of all actions (events) that occurred after the specified block, effectively undoing those changes.
    - **Timestamp Adjustment**: Resets the internal `_currentTimestamp` to the timestamp of the specified block.
- **Use Cases**: Handling blockchain reorganizations or rolling back to a known good state for analysis.