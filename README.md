Bolt-Physics

Bolt is required, but since bolt is a paid asset, it is not included.  Place your own bolt.dll in Assets/bolt/assemblies

Project built with bolt 1.0.0.4 and Unity 2017.1b8.
See the bolt slack for getting bolt to work with Unity 2017.1b8.

https://www.youtube.com/watch?v=F69be7VOutg

I needed a solution for my game Party Panic that would allow for a few things:
1) Physics need to stay in sync across all clients
2) Client-side prediction for (four) player controlled physics objects (moveable spheres) need to be responsive
3) Players need to be able to collide with other players and knock them back. This knockback needs to be responsive to the client, be physically accurate (ish) and to generally just feel nice.
   

Quick rundown of how it works;
There's two main steps. 
1) Rewind/Resim local physics from the last server validated physics frame
2) The main physics loop. This loop should only call one step every frame, this is the up-to-date realtime physics simulation. If we were to remove step (1), it would behave like unity was auto-stepping physics normally (mostly)

Starting with step (2).  
- If we haven't simulated up to the current server frame...
- Store the state of all 'rewindables' at this frame before the physics step. Every physics object should be a rewindable (currently).  This just stores it's pos/rot/vel/avel in a dict using the frame as a key.
- Poll all 'controllable rewindables' for inputs.(*1)
- Step Physics one frame (Physics.Simulate(Time.fixedDeltaTime)
- increment our current frame

(*1) - When we poll for inputs we actually do a few things.
1) Listen for inputs if we're the owner
2) Pack the inputs into an event and send it off to the server
3) Store the input in a frame-stamped list (so when we resimulate this frame we can apply the input that happened at this frame)
4) Apply the input instantly so that controls feel responsive

When the server receives the input event, it stores it in a frame-stamped list too.
Once we have received inputs from all players for this frame, we can validate it.  (We could probably send out validations whenever we get a new input instead of waiting for all inputs so we don't hang when one input takes a very long time to be received)

When we're ready to validate a frame we:
1) Rewind the servers simulation to the previous validation frame.
2) Prepare to step the simulation one frame.
3) Store the state of all rewindables for this new frame, overwritting any old (unvalidated states)
4) Apply all recieved player-input for this frame.
5) Step the physics one step
6) goto 2 and repeat until we've validated the simulation up the the new validation frame.

We then broadcast out events to all rewindables notifying them of their new (validated) state. (this could be sent based on priority instead of sending it to everything, as we don't need every rewindable to all be on the same validated frame)

Then we Resimulate from validationFrame->currentFrame to make sure the server player is seeing the most recent versio of the world.  During this resimulation we also reapply any stored local inputs that happened on these frames.

Step (1) deals with the client resimulating itself when a correction is needed (the local simulation has drifted too far from where the server thinks it should be)
When we receive a validation event for a rewindable it:

1) store the validated state (pos/rot/vel/avel) in a frame-stamped list.  state data in this list is preferred over the local simulated state list.
2) Since we've already locally simulated this frame and stored it's state we can compare the validated state against the locally simulated state, even though they both happened 'in the past'.
3) Here we can compare the states and see how different they are, and decide if they are too different and need a resim to fix (using the validated data as a starting point for the resim)
4) If we decide it needs a resim we mark the PhysicsManager.

Back to our main physics loop, the resimulation happens BEFORE it.
1) If we need a resim
2) Rewind our local sim to the validated starting frame
3) Apply any locally stored inputs for this frame
4) Step the physics
5) Try and SNAP the post-simulated state to the validated state if we can (if we have the validated for an object).  If we don't thats ok, but if we do this will make the simulation stay a little bit closer to the servers sim.

There's a bunch of little things all over the place to make things a little bit better.  The code is messy, but has tons of comments as I was working through getting this to work.

I'm not sure how well this will work with many bodies, as the more bodies you need to sim the longer each call to Physics.Simulate will take, and it adds up quick when you're rewinding/resimulating lots of steps every fixedupated.  This should work (or easy to modify to work) with non-controlled rigidbodies in the scene as well as controlled-rigidbodies.

Code is free to use as is.  If you have questions @DMeville on twitter
