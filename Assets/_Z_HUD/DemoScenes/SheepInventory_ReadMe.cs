// README FILE FOR SHEEP INVENTORY DEMO SCENE
// 
// This demo scene is designed to showcase the functionality of the Sheep Inventory system.


// THE DESIGN:
// 
// This inventory is designed to keep the sheep out of the way of the player.
// It gives the illusion that all the sheep are located behind the camera. They can be called and returned as needed.
// The sheep have an animation that guides them from behind the camera to the player, and a small jump into the player's arms.
// Sheep returning to their original position behind the camera will also have an animation that guides them back to their original position.
// If a sheep gets stuck, they will timeout and jump to the location they are supposed to be.


// THE CONTROLS FOR THE SHEEP INVENTORY:
//
// Click a button in the HUD to call that sheep, or press numbers 1-5 to call a sheep.
// ALT  ::  Display the inventory HUD while holding down ALT.
// E    ::  Drop the currently held sheep, or Pick up the nearest sheep if you are not holding one.
// R    ::  Return the currently held sheep to the inventory.
// Q    ::  Return all sheep in a wide radius to the inventory.


// OTHER CONTROLS:
//
// WASD  ::  Move the player around.
// Right click and drag  ::  Rotate the camera around the player.


// THINGS TO NOTE:
//
// There has been no styling done to this demo. If the functionality is approved(for now), the Art team can design what this HUD will look like.
//
// The only parts of this demo meant to be kept are:
// -    InventoryHUD prefab
// -    Script files that do NOT start with "Demo"
//
// The rest is just stuff needed to demonstrate the functionality of the inventory system, and can be deleted later.