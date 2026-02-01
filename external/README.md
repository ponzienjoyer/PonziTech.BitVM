# External Dependencies

This directory contains external dependencies as git submodules.

## BitVM

The BitVM repository is included as a submodule:

```bash
git submodule add https://github.com/BitVM/BitVM.git external/BitVM
git submodule update --init --recursive
```

To initialize after cloning:

```bash
git submodule update --init --recursive
```

To update the submodule to latest:

```bash
cd external/BitVM
git pull origin main
cd ../..
git add external/BitVM
git commit -m "Update BitVM submodule"
```

## Okeanos.NBitcoin (Optional)

For Okeanos flavor builds, place your NBitcoin fork at:

```
external/NBitcoin/
```

This should be a project reference, not a submodule, as it's your internal fork.
