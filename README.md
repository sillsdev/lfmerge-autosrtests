# Automated LfMerge S/R Tests
<!-- markdownlint-disable MD024 -->
<!-- markdownlint-disable MD029 -->

**NOTE:** This project requires Mono 5.x, i.e. the package `mono5-sil` has to be installed!

The mocked LanguageDepot/Chorus repo as well as the mongo database data are stored
in the source tree as series of patch files in the data directory. The Chorus repo
and the mongo database will be restored during a test run.

The data is stored as patch files rather than in binary format so that it is
possible to easily see the difference between two versions of the test data.

## Understanding the tests

The existing tests use three different objects: `_mongo` is the mongo database and contains
the data that a user would see on the _Language Forge_ website, `_languageDepot` represents
the Chorus repo on Language Depot and contains the data after FW did a send/receive. And
finally `_webwork` which represents the Chorus repo that _lfmerge_ will work with. It contains
the data after the last s/r happened, before any changes were made in either _Language Forge_
(i.e. `_mongo`) or FW (i.e. `_languageDepot`).

## Creating new tests - using guided tool

### Prerequisites

- local LanguageForge accessible at `http://languageforge.local/`
- FieldWorks executable for all model versions: checkout FW source code for
  _modelVersion_, build FW, then copy the `Output_x86_64/` and `DistFiles`
  directories to `Output<modelVersion>/`, e.g. `Output7000068/` (you can omit
  `DistFiles/{ReleaseData,Helps,Projects,Language Explorer/Movies}`)
- Install `flexbridge` either in `/usr/lib/flexbridge`, or create a symlink
  named `flexbridge` in `Ouput<modelVersion>/Output_$(uname -m)` that points
  to your `flexbridge` directory

NOTE: in order to be able to run multiple FW versions from different directories
without having to checkout the corresponding source code version, I had to delete
various `values.xml` files in `~/.mono/registry/CurrentUser/software/sil` and
manually add an entry for `RootCodeDir` pointing to `Output<modelVersion>/DistFiles`
to `Output<modelVersion>/Output_x86_64/registry/LocalMachine/software/sil/fieldworks/8/values.xml`

### Creating new test data

The `TestUtil` test utility has a wizard mode that guides through the steps
necessary to create test data for a new unit test (with a USB stick mounted at
`/media/$USER/MyUsbStick`):

```bash
mono --debug TestUtil.exe wizard --mongo=2 --ld=2 --project autosrtests \
	--fwroot=$HOME/fwrepo/fw --usb /media/$USER/MyUsbStick --datadir=data \
	--msg "new test"
```

## Creating new tests - semi-automated way

### Restore existing Chorus repo

To restore a Chorus repo up to revision _rev_, run the following command. The patch
files (up to revision _rev_; in the example below this would be `r0.patch` and `r1.patch`) are taken from the
`data/7000068` directory and the Chorus repo will end up in
`/tmp/testdata/LanguageDepot`.

```bash
mono --debug TestUtil.exe restore --ld=1 --workdir=/tmp/testdata/ \
	--project=autosrtests --model=7000068 --datadir=data
```

### Export existing Chorus repo

Make the changes in the lowest supported version of FLEx (currently 8.2.9). Then
run send/receive to a USB stick. Afterwards copy the directory from the USB stick to
your hard drive (e.g. /tmp/ld) to speed up processing.

The new changes can be exported by running:

```bash
mono --debug TestUtil.exe save --ld --workdir /tmp/ld --project autosrtests \
	--model 7000068 --datadir=otherdata
```

This will save the new patches in `otherdata/7000068`.

**NOTE:** When doing a S/R in FLEx to the USB stick be aware that _flexbridge_ identifies
the correct directory by checking the hash of the first commit. If multiple repos
exist with the same history it is unclear which directory _flexbridge_
chooses, so you might get unexpected results.

### Restore test project in Mongo

The patches for the test project in the mongo database are located as patches in
`data/<modelversion>/mongo`. To restore version _2_ for model version
_7000068_ run the following command:

```bash
mono --debug TestUtil.exe restore --mongo=2 --project autosrtests \
	--workdir /tmp/testdata --model 7000068 --datadir=data
```

### Export test project in Mongo

Make the changes in _LanguageForge_, then run:

```bash
mono --debug TestUtil.exe save --mongo --project autosrtests --msg "commit msg" \
	--workdir /tmp/testdata --model 7000068 --datadir=data
```

### Merge Chorus and Mongo test data

Before writing a new test you'll have to created the merged data, i.e. get the data
in the state it would be after a send/receive. This is necessary because currently
the patches are consecutive (if necessary this could be changed by introducing an
additional path parameter).

The merged data based on mongo patch _2_ and
language depot patch _2_ for model version 7000068 can be created by running:

```bash
mono --debug TestUtil.exe merge --mongo=2 --project autosrtests \
	--ld=2 --workdir=/tmp/testdata/ --model=7000068 --datadir=data
```

### Steps to create a new test

To create a new test you'll need to make the necessary changes in FieldWorks
and LF, then export the changes and write the unit test.

#### Create changes in LanguageDepot/Chorus

1. Restore the Chorus repo:

```bash
mono --debug TestUtil.exe restore --ld=3 --project autosrtests \
	--workdir /tmp/testdata --model 7000068 --datadir=data
```

2. Copy the `.hg` subdirectory from `/tmp/testdata/LanguageDepot` to the USB stick

```bash
cp -a /tmp/testdata/LanguageDepot/.hg /media/$USER/USBstick/test-7000068
```

3. In FW get the testproject from the USB stick. This might involve deleting the
   existing project in FW. If you re-use the existing project make sure that later
   on it doesn't create extra commits in the Chorus repo.

4. Make the desired changes in FW

5. S/R from FLEx to the USB stick

6. Copy the `test-7000068` folder from the USB stick to the `/tmp` directory

7. Export the new commit of the simulated LanguageDepot by running:

```bash
mono --debug TestUtil.exe save --ld --workdir /tmp/test-7000068 \
	--project autosrtests --model 7000068 --datadir=data
```

#### Create changes in LanguageForge

1. Restore project in Mongo

```bash
mono --debug TestUtil.exe restore --mongo=3 --project autosrtests \
	--workdir /tmp/testdata --model 7000068 --datadir=data
```

2. Make changes in local LF

3. Export Mongo test data

```bash
mono --debug TestUtil.exe save --mongo --project autosrtests \
	--msg "commit msg" --workdir /tmp/testdata --model 7000068 \
	--datadir=data
```

#### Changes in `LfMerge.AutomatedSRTests`

- create the new unit test
- repeat the above steps for each model version
- merge the Chorus and Mongo test data for each model version:

```bash
mono --debug TestUtil.exe merge --mongo=2 --project autosrtests \
	--ld=2 --workdir=/tmp/testdata/ --model=7000068 --datadir=data
```

## Creating new tests - the hard way

### Export existing Chorus repo

```bash
mkdir -p $modelversion
for i in $(hg log -b $modelversion --template "{rev} "); do
	hg export -r $i > $modelversion/r$i.patch
done
```

### Restore existing Chorus repo

The mocked LanguageDepot/Chorus repo is stored in the source tree as a series of
patch files in the `data` directory. The repo will be restored during a test run.
However, for adding or modifying tests it might be necessary to manually restore the
repo. This can be achieved by creating a Mercurial repo and then applying the patches:

```bash
mkdir -p workdir/testproj
hg init .
hg import ../../data/r0.patch
hg import ../../data/7000068/r1.patch
etc.
```

### Export Mongo database

```bash
mongoexport --db scriptureforge --collection projects --query '{ "projectName" : $projectname}' > $projectname.json
for col in activity lexicon optionlists; do
	mongoexport --db $projectname --collection $col > ${projectname}.${col}.json
done
```

### Restore Mongo database

```bash
mongoimport --db scriptureforge --collection projects --file $dbname.json
for col in activity lexicon optionlists; do
	mongoimport --db $dbname --drop --collection $col --file ${dbname}.${col}.json
done
```
